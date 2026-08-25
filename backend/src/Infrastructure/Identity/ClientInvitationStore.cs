using Application.Common.Abstractions;
using Application.Features.Authentication.Abstractions;
using Domain.Entities.Identity;
using Domain.ValueObjects;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Identity;

/// <summary>Emite convites e confirma transferência entre tenants atomicamente.</summary>
public sealed class ClientInvitationStore : IClientInvitationStore
{
    private readonly PtManagerDbContext _dbContext;
    private readonly IOpaqueTokenService _tokens;
    private readonly ITenantContext _tenantContext;

    public ClientInvitationStore(
        PtManagerDbContext dbContext,
        IOpaqueTokenService tokens,
        ITenantContext tenantContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
    }

    public async Task<IssueClientInvitationStoreResult> IssueAsync(
        Guid trainerId,
        Guid clientId,
        string email,
        DateTime expiresAt,
        DateTime now,
        CancellationToken cancellationToken
    )
    {
        await using var transaction = await _dbContext.Database
            .BeginTransactionAsync(cancellationToken);
        var client = await _dbContext.Clients
            .FromSqlInterpolated(
                $"SELECT * FROM clients WHERE owner_trainer_id = {trainerId} AND id = {clientId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (client is null)
            return IssueClientInvitationStoreResult.For(
                IssueClientInvitationStoreStatus.ClientNotFound);
        if (!client.IsActive || client.IsDeleted)
            return IssueClientInvitationStoreResult.For(
                IssueClientInvitationStoreStatus.ClientInactive);

        var address = new EmailAddress(email);
        if (client.NormalizedContactEmail != address.Normalized)
            return IssueClientInvitationStoreResult.For(
                IssueClientInvitationStoreStatus.EmailMismatch);
        if (client.UserId.HasValue)
            return IssueClientInvitationStoreResult.For(
                IssueClientInvitationStoreStatus.RelationshipConflict);

        var previous = await _dbContext.InviteTokens
            .Where(token => token.TrainerId == trainerId &&
                token.ClientId == clientId &&
                token.UsedAt == null)
            .ToListAsync(cancellationToken);
        _dbContext.InviteTokens.RemoveRange(previous);

        var generated = _tokens.Generate();
        _dbContext.InviteTokens.Add(new InviteToken(
            trainerId,
            clientId,
            address,
            generated.TokenHash,
            expiresAt,
            now
        ));

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return IssueClientInvitationStoreResult.Issued(
                new IssuedAuthenticationSecret(address.Value, generated.RawToken, expiresAt));
        }
        catch (DbUpdateConcurrencyException)
        {
            return IssueClientInvitationStoreResult.For(
                IssueClientInvitationStoreStatus.ConcurrencyConflict);
        }
    }

    public async Task<ConsumeClientInvitationStoreResult> ConsumeAsync(
        string rawToken,
        Guid authenticatedUserId,
        bool transferApproved,
        DateTime refreshExpiresAt,
        DateTime now,
        CancellationToken cancellationToken
    )
    {
        if (_tenantContext.UserId != authenticatedUserId || _tenantContext.Role != "client")
            return ConsumeClientInvitationStoreResult.For(
                ConsumeClientInvitationStoreStatus.AccountInactive);

        var hash = _tokens.Hash(rawToken);
        await using var transaction = await _dbContext.Database
            .BeginTransactionAsync(cancellationToken);

        var invite = await _dbContext.InviteTokens
            .FromSqlInterpolated(
                $"SELECT * FROM invite_tokens WHERE token_hash = {hash} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

        if (invite is null)
            return ConsumeClientInvitationStoreResult.For(
                ConsumeClientInvitationStoreStatus.TokenNotFound);
        if (invite.UsedAt is not null)
            return ConsumeClientInvitationStoreResult.For(
                ConsumeClientInvitationStoreStatus.TokenAlreadyConsumed);
        if (invite.ExpiresAt <= now)
            return ConsumeClientInvitationStoreResult.For(
                ConsumeClientInvitationStoreStatus.TokenExpired);

        var user = await _dbContext.Users
            .FromSqlInterpolated($"SELECT * FROM users WHERE id = {authenticatedUserId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (user is null || user.Role != "client" || !user.IsActive || user.IsDeleted)
            return ConsumeClientInvitationStoreResult.For(
                ConsumeClientInvitationStoreStatus.AccountInactive);
        if (!string.Equals(user.NormalizedEmail, new EmailAddress(invite.Email).Normalized,
            StringComparison.Ordinal))
            return ConsumeClientInvitationStoreResult.For(
                ConsumeClientInvitationStoreStatus.EmailMismatch);

        var target = await _dbContext.Clients
            .FromSqlInterpolated(
                $"SELECT * FROM clients WHERE owner_trainer_id = {invite.TrainerId} AND id = {invite.ClientId} FOR UPDATE")
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(cancellationToken);

        if (target is null || target.IsDeleted || !target.IsActive || target.UserId.HasValue &&
            target.UserId != user.Id)
            return ConsumeClientInvitationStoreResult.For(
                ConsumeClientInvitationStoreStatus.RelationshipConflict);

        var active = await _dbContext.Clients
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(client => client.UserId == user.Id && client.IsActive && !client.IsDeleted)
            .Select(client => new { client.Id, client.OwnerTrainerId })
            .Take(2)
            .ToListAsync(cancellationToken);
        if (active.Count > 1)
            return ConsumeClientInvitationStoreResult.For(
                ConsumeClientInvitationStoreStatus.RelationshipConflict);

        var source = active.SingleOrDefault(client => client.Id != target.Id ||
            client.OwnerTrainerId != target.OwnerTrainerId);
        if (source is not null && !transferApproved)
            return ConsumeClientInvitationStoreResult.For(
                ConsumeClientInvitationStoreStatus.TransferApprovalRequired);

        var trainerIds = new[] { target.OwnerTrainerId, source?.OwnerTrainerId }
            .Where(trainer => trainer.HasValue)
            .Select(trainer => trainer!.Value)
            .Distinct()
            .Order()
            .ToArray();

        await _dbContext.TrainerSubscriptions
            .FromSqlInterpolated(
                $"SELECT * FROM trainer_subscriptions WHERE trainer_id = ANY({trainerIds}) ORDER BY trainer_id FOR UPDATE")
            .IgnoreQueryFilters()
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        if (source is not null)
        {
            var sourceRows = await _dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE clients SET is_active = false, updated_at = {now} WHERE id = {source.Id} AND owner_trainer_id = {source.OwnerTrainerId} AND user_id = {user.Id} AND is_active = TRUE AND is_deleted = FALSE",
                cancellationToken);
            if (sourceRows != 1)
                return ConsumeClientInvitationStoreResult.For(
                    ConsumeClientInvitationStoreStatus.ConcurrencyConflict);
            await _dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE trainer_subscriptions SET current_client_count = GREATEST(current_client_count - 1, 0), updated_at = {now} WHERE trainer_id = {source.OwnerTrainerId}",
                cancellationToken);
        }
        if (!target.UserId.HasValue)
        {
            var targetRows = await _dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE clients SET user_id = {user.Id}, updated_at = {now} WHERE id = {target.Id} AND owner_trainer_id = {target.OwnerTrainerId} AND user_id IS NULL AND is_active = TRUE AND is_deleted = FALSE",
                cancellationToken);
            if (targetRows != 1)
                return ConsumeClientInvitationStoreResult.For(
                    ConsumeClientInvitationStoreStatus.ConcurrencyConflict);
        }
        invite.MarkUsed(now);
        if (!user.EmailConfirmed)
            user.ConfirmEmail(now);

        var sessions = await _dbContext.RefreshTokens
            .Where(token => token.UserId == user.Id && token.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var session in sessions)
            session.Revoke(now);

        if (source is not null)
            _dbContext.TenantTransferAudits.Add(new TenantTransferAudit(
                user.Id,
                source.OwnerTrainerId,
                target.OwnerTrainerId,
                target.Id,
                now
            ));

        var generated = _tokens.Generate();
        _dbContext.RefreshTokens.Add(new RefreshToken(
            user.Id,
            Guid.NewGuid(),
            generated.TokenHash,
            null,
            refreshExpiresAt,
            now
        ));

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            var principal = new AuthenticatedPrincipal(
                user.Id,
                target.OwnerTrainerId,
                user.Role,
                user.SecurityStamp
            );
            return ConsumeClientInvitationStoreResult.Accepted(
                principal,
                new IssuedRefreshSession(generated.RawToken, refreshExpiresAt));
        }
        catch (DbUpdateConcurrencyException)
        {
            return ConsumeClientInvitationStoreResult.For(
                ConsumeClientInvitationStoreStatus.ConcurrencyConflict);
        }
        catch (DbUpdateException)
        {
            return ConsumeClientInvitationStoreResult.For(
                ConsumeClientInvitationStoreStatus.RelationshipConflict);
        }
    }
}
