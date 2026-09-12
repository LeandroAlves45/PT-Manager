using System.Text.Json;
using Application.Features.ClientPortal.Abstractions;
using Domain.Entities.Jobs;
using Infrastructure.Data;
using Infrastructure.Persistence.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Infrastructure.Persistence.ClientPortal;

/// <summary>
/// Escreve o avatar do cliente autenticado e agenda a eliminação do asset
/// anterior na mesma transação.
/// </summary>
internal sealed class MyAvatarStore : IMyAvatarStore
{
    internal const string DeletionMessageType = "client-avatar.delete";

    private readonly PtManagerDbContext _dbContext;

    public MyAvatarStore(PtManagerDbContext dbContext) =>
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    public Task<MyAvatarOutcome> ReplaceAsync(
        Guid trainerId,
        Guid clientUserId,
        string avatarUrl,
        string avatarPublicId,
        Guid correlationId,
        DateTime now,
        CancellationToken cancellationToken) =>
        ExecuteInTransactionAsync(async transaction =>
        {
            var clientId = await _dbContext.LockOwnClientAsync(
                trainerId, clientUserId, cancellationToken);
            if (clientId == Guid.Empty)
            {
                await transaction.CommitAsync(cancellationToken);
                return MyAvatarOutcome.NotFound;
            }

            var client = await _dbContext.Clients
                .SingleAsync(candidate => candidate.Id == clientId, cancellationToken);

            // Uma repetição da mesma operação após falha transitória não pode
            // agendar a eliminação do asset que continua ativo.
            if (string.Equals(client.AvatarUrl, avatarUrl, StringComparison.Ordinal) &&
                string.Equals(client.AvatarPublicId, avatarPublicId, StringComparison.Ordinal))
            {
                await transaction.CommitAsync(cancellationToken);
                return MyAvatarOutcome.Updated(MyProfileMapping.ToDto(client));
            }

            var previousPublicId = client.ReplaceAvatar(avatarUrl, avatarPublicId, now);

            if (previousPublicId is not null &&
                !string.Equals(previousPublicId, avatarPublicId, StringComparison.Ordinal))
                EnqueueAvatarDeletion(trainerId, previousPublicId, correlationId, now);

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return MyAvatarOutcome.Updated(MyProfileMapping.ToDto(client));
        }, cancellationToken);

    public Task<MyAvatarOutcome> RemoveAsync(
        Guid trainerId,
        Guid clientUserId,
        Guid correlationId,
        DateTime now,
        CancellationToken cancellationToken) =>
        ExecuteInTransactionAsync(async transaction =>
        {
            var clientId = await _dbContext.LockOwnClientAsync(
                trainerId, clientUserId, cancellationToken);
            if (clientId == Guid.Empty)
            {
                await transaction.CommitAsync(cancellationToken);
                return MyAvatarOutcome.NotFound;
            }

            var client = await _dbContext.Clients
                .SingleAsync(candidate => candidate.Id == clientId, cancellationToken);

            var previousPublicId = client.RemoveAvatar(now);
            if (previousPublicId is not null)
                EnqueueAvatarDeletion(trainerId, previousPublicId, correlationId, now);

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return MyAvatarOutcome.Updated(MyProfileMapping.ToDto(client));
        }, cancellationToken);

    /// <summary>
    /// Agenda a eliminação do asset anterior. A chave de idempotência deriva da
    /// correlação da mutação e tem tamanho fixo, independente do identificador
    /// devolvido pelo storage.
    /// </summary>
    private void EnqueueAvatarDeletion(
        Guid trainerId,
        string previousPublicId,
        Guid correlationId,
        DateTime now)
    {
        var payload = JsonSerializer.Serialize(new { public_id = previousPublicId });
        _dbContext.OutboxMessages.Add(new OutboxMessage(
            trainerId,
            DeletionMessageType,
            payload,
            $"{DeletionMessageType}:{correlationId:N}",
            correlationId,
            now));
    }

    private Task<MyAvatarOutcome> ExecuteInTransactionAsync(
        Func<IDbContextTransaction, Task<MyAvatarOutcome>> operation,
        CancellationToken cancellationToken)
    {
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(async () =>
        {
            _dbContext.ChangeTracker.Clear();
            await using var transaction = await _dbContext.Database
                .BeginTransactionAsync(cancellationToken);

            try
            {
                return await operation(transaction);
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw;
            }
        });
    }
}
