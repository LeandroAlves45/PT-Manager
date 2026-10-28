using System.Text.Json;
using Application.Features.ClientPortal.Abstractions;
using Domain.Entities.Clients;
using Domain.Entities.Identity;
using Domain.Entities.Jobs;
using Infrastructure.Data;
using Infrastructure.Persistence.Clients;
using Infrastructure.Persistence.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.ClientPortal;

/// <summary>
/// Elimina a conta do cliente autenticado: anonimiza e mantém o histórico.
/// </summary>
/// <remarks>
/// <para>
/// Tudo numa transação, para nunca ficar meio eliminado: a conta (credenciais, email, nome,
/// ligação ao Google, tokens), a ficha ativa (com os efeitos do arquivo pelo personal
/// trainer) e o texto livre escrito pelo cliente. Os registos de treino, medidas e
/// avaliações ficam no tenant do personal trainer, ligados a uma ficha que já não identifica
/// ninguém.
/// </para>
/// <para>
/// Um cliente transferido tem fichas antigas noutros tenants, arquivadas na transferência.
/// Também são anonimizadas, com escritas diretas filtradas pela conta do próprio cliente:
/// os dados são dele, mesmo estando num tenant que já não é o seu.
/// </para>
/// </remarks>
internal sealed class MyAccountDeletionStore : IMyAccountDeletionStore
{
    /// <summary>Texto que substitui campos de texto livre obrigatórios.</summary>
    internal const string RemovedText = "Removido";

    private readonly PtManagerDbContext _dbContext;
    private readonly UserManager<User> _userManager;

    public MyAccountDeletionStore(PtManagerDbContext dbContext, UserManager<User> userManager)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
    }

    public Task<MyAccountDeletionStatus> DeleteAsync(
        Guid trainerId,
        Guid clientUserId,
        string? password,
        Guid correlationId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        var attempt = 0;
        return strategy.ExecuteAsync(async () =>
        {
            attempt++;
            _dbContext.ChangeTracker.Clear();

            // Um commit pode ter chegado à base de dados antes de a ligação cair. Repetir
            // daria NotFound (a conta já não existe) e o cliente veria um erro de uma
            // eliminação que correu bem: confirma-se primeiro.
            if (attempt > 1 && await IsAlreadyDeletedAsync(clientUserId, cancellationToken))
                return MyAccountDeletionStatus.Deleted;

            await using var transaction = await _dbContext.Database
                .BeginTransactionAsync(cancellationToken);

            try
            {
                var status = await DeleteOnceAsync(
                    trainerId, clientUserId, password, correlationId, now, cancellationToken);

                // A falha de password incrementa o contador de lockout: também é gravada.
                if (status is MyAccountDeletionStatus.Deleted or MyAccountDeletionStatus.PasswordInvalid)
                    await transaction.CommitAsync(cancellationToken);
                else
                    await transaction.RollbackAsync(CancellationToken.None);

                return status;
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw;
            }
        });
    }

    private async Task<MyAccountDeletionStatus> DeleteOnceAsync(
        Guid trainerId,
        Guid clientUserId,
        string? password,
        Guid correlationId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var clientId = await _dbContext.LockOwnClientAsync(trainerId, clientUserId, cancellationToken);
        if (clientId == Guid.Empty)
            return MyAccountDeletionStatus.NotFound;

        var user = await _dbContext.Users
            .FromSqlInterpolated($"SELECT * FROM users WHERE id = {clientUserId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (user is null || user.IsDeleted || user.Role != "client")
            return MyAccountDeletionStatus.NotFound;

        var passwordStatus = await VerifyPasswordAsync(user, password);
        if (passwordStatus is not null)
            return passwordStatus.Value;

        // 1. Ficha ativa: efeitos do arquivo (lugar no plano, sessões futuras, packs,
        //    convites, sessões de login) e anonimização pela regra do domínio.
        if (!await ClientArchiveEffects.ReleaseSubscriptionSlotAsync(_dbContext, trainerId, now, cancellationToken))
            throw new InvalidOperationException("The client's personal trainer has no subscription.");
        await ClientArchiveEffects.ApplyAsync(_dbContext, clientId, trainerId, now, cancellationToken);

        var client = await _dbContext.Clients
            .SingleAsync(candidate => candidate.Id == clientId, cancellationToken);
        var avatarPublicId = client.Anonymize(now);
        if (avatarPublicId is not null)
            EnqueueAvatarDeletion(trainerId, clientId, avatarPublicId, correlationId, now);

        // 2. Fichas antigas noutros tenants (cliente transferido), já arquivadas.
        var previousClients = await _dbContext.Clients
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(candidate => candidate.UserId == clientUserId && candidate.Id != clientId)
            .Select(candidate => new { candidate.Id, candidate.OwnerTrainerId, candidate.BirthDate, candidate.AvatarPublicId })
            .ToListAsync(cancellationToken);
        foreach (var previous in previousClients)
        {
            await AnonymizePreviousClientAsync(previous.Id, previous.BirthDate, now, cancellationToken);
            if (previous.AvatarPublicId is not null)
                EnqueueAvatarDeletion(previous.OwnerTrainerId, previous.Id, previous.AvatarPublicId, correlationId, now);
        }

        // 3. Texto livre escrito pelo cliente e emails guardados, em todas as fichas.
        var clientIds = previousClients.Select(previous => previous.Id).Append(clientId).ToList();
        await ScrubClientFreeTextAsync(clientIds, user, now, cancellationToken);

        // 4. A conta: credenciais, ligação ao Google, tokens de email e sessões.
        await _dbContext.Set<ExternalIdentity>()
            .Where(identity => identity.UserId == clientUserId)
            .ExecuteDeleteAsync(cancellationToken);
        await _dbContext.EmailVerificationTokens
            .Where(token => token.UserId == clientUserId)
            .ExecuteDeleteAsync(cancellationToken);
        await _dbContext.PasswordResetTokens
            .Where(token => token.UserId == clientUserId)
            .ExecuteDeleteAsync(cancellationToken);
        await _dbContext.RefreshTokens
            .Where(token => token.UserId == clientUserId && token.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.RevokedAt, now), cancellationToken);

        user.DeleteAndAnonymize(now);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return MyAccountDeletionStatus.Deleted;
    }

    private Task<bool> IsAlreadyDeletedAsync(Guid clientUserId, CancellationToken cancellationToken)
    {
        var anonymizedEmail = AnonymizedEmailFor(clientUserId);
        return _dbContext.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(user => user.Id == clientUserId && user.IsDeleted && user.Email == anonymizedEmail, cancellationToken);
    }

    private static string AnonymizedEmailFor(Guid userId) => $"deleted-{userId:N}@deleted.invalid";

    /// <summary>
    /// Mesma regra do login: conta bloqueada responde como password errada (sem revelar o
    /// estado) e cada falha conta para o bloqueio. Uma conta sem password (só Google) não
    /// tem nada a confirmar além da palavra de confirmação.
    /// </summary>
    private async Task<MyAccountDeletionStatus?> VerifyPasswordAsync(User user, string? password)
    {
        if (user.PasswordHash is null)
            return null;
        if (password is null)
            return MyAccountDeletionStatus.PasswordRequired;
        if (await _userManager.IsLockedOutAsync(user))
            return MyAccountDeletionStatus.PasswordInvalid;
        if (await _userManager.CheckPasswordAsync(user, password))
            return null;

        await _userManager.AccessFailedAsync(user);
        return MyAccountDeletionStatus.PasswordInvalid;
    }

    private Task AnonymizePreviousClientAsync(
        Guid clientId,
        Domain.ValueObjects.BirthDate birthDate,
        DateTime now,
        CancellationToken cancellationToken) =>
        _dbContext.Clients
            .IgnoreQueryFilters()
            .Where(candidate => candidate.Id == clientId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(candidate => candidate.Name, Client.AnonymizedName)
                    .SetProperty(candidate => candidate.ContactEmail, (string?)null)
                    .SetProperty(candidate => candidate.NormalizedContactEmail, (string?)null)
                    .SetProperty(candidate => candidate.Phone, Client.AnonymizedPhoneFor(clientId))
                    .SetProperty(candidate => candidate.BirthDate, Client.AnonymizedBirthDate(birthDate))
                    .SetProperty(candidate => candidate.Objective, (string?)null)
                    .SetProperty(candidate => candidate.Notes, (string?)null)
                    .SetProperty(candidate => candidate.EmergencyContactName, (string?)null)
                    .SetProperty(candidate => candidate.EmergencyContactPhone, (string?)null)
                    .SetProperty(candidate => candidate.AvatarUrl, (string?)null)
                    .SetProperty(candidate => candidate.AvatarPublicId, (string?)null)
                    .SetProperty(candidate => candidate.SelectedMealPlanId, (Guid?)null)
                    .SetProperty(candidate => candidate.IsActive, false)
                    .SetProperty(candidate => candidate.UpdatedAt, now),
                cancellationToken);

    /// <summary>
    /// Apaga o texto livre que o cliente escreveu (condições médicas, profissão, objetivos, notas de
    /// check-in, de séries e de treinos concluídos) e os emails guardados em convites e
    /// notificações. Os números (peso, medidas, cargas) ficam: são o histórico do treino.
    /// </summary>
    private async Task ScrubClientFreeTextAsync(
        IReadOnlyCollection<Guid> clientIds,
        User user,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var anonymizedEmail = AnonymizedEmailFor(user.Id);

        await _dbContext.InitialAssessments
            .IgnoreQueryFilters()
            .Where(assessment => clientIds.Contains(assessment.ClientId))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(assessment => assessment.MedicalConditions, (string?)null)
                    .SetProperty(assessment => assessment.Profession, (string?)null)
                    .SetProperty(assessment => assessment.Goals, RemovedText)
                    .SetProperty(assessment => assessment.UpdatedAt, now),
                cancellationToken);

        await _dbContext.CheckIns
            .IgnoreQueryFilters()
            .Where(checkIn => clientIds.Contains(checkIn.ClientId) && checkIn.Notes != null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(checkIn => checkIn.Notes, (string?)null)
                    .SetProperty(checkIn => checkIn.UpdatedAt, now),
                cancellationToken);

        await _dbContext.ClientExerciseSetLogs
            .IgnoreQueryFilters()
            .Where(log => clientIds.Contains(log.ClientId) && log.Notes != null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(log => log.Notes, (string?)null), cancellationToken);

        await _dbContext.WorkoutCompletions
            .IgnoreQueryFilters()
            .Where(completion => clientIds.Contains(completion.ClientId) && completion.Notes != null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(completion => completion.Notes, (string?)null),
                cancellationToken);

        await _dbContext.InviteTokens
            .IgnoreQueryFilters()
            .Where(token => clientIds.Contains(token.ClientId))
            .ExecuteDeleteAsync(cancellationToken);

        // Por enviar: deixam de sair. Enviadas: ficam como registo, sem o endereço nem os dados.
        await _dbContext.Notifications
            .IgnoreQueryFilters()
            .Where(notification =>
                notification.ClientId.HasValue && clientIds.Contains(notification.ClientId.Value) &&
                (notification.Status == "pending" || notification.Status == "failed"))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(notification => notification.Status, "skipped")
                    .SetProperty(notification => notification.UpdatedAt, now),
                cancellationToken);
        await _dbContext.Notifications
            .IgnoreQueryFilters()
            .Where(notification =>
                notification.ClientId.HasValue && clientIds.Contains(notification.ClientId.Value))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(notification => notification.RecipientEmail, anonymizedEmail)
                    .SetProperty(notification => notification.TemplateData, (string?)null)
                    .SetProperty(notification => notification.UpdatedAt, now),
                cancellationToken);
    }

    /// <summary>
    /// Agenda a eliminação do avatar no armazenamento, pela outbox existente, no tenant onde o
    /// ficheiro vive. A chave junta a operação e a ficha: uma por avatar, estável num retry.
    /// </summary>
    private void EnqueueAvatarDeletion(Guid trainerId, Guid clientId, string publicId, Guid correlationId, DateTime now)
    {
        var payload = JsonSerializer.Serialize(new { public_id = publicId });
        _dbContext.OutboxMessages.Add(new OutboxMessage(
            trainerId,
            MyAvatarStore.DeletionMessageType,
            payload,
            $"{MyAvatarStore.DeletionMessageType}:{correlationId:N}:{clientId:N}",
            correlationId,
            now));
    }
}
