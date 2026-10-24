using System.Data;
using System.Text.Json;
using Application.Features.Administration.Trainers.Abstractions;
using Application.Features.Administration.Trainers.Dtos;
using Domain.Entities.Administration;
using Domain.Entities.Billing;
using Domain.Entities.Identity;
using Domain.ValueObjects;
using Infrastructure.Data;
using Infrastructure.Email;
using Infrastructure.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Infrastructure.Persistence.Administration;

/// <summary>
/// Escritas do superuser sobre treinadores (RN-008). Cada tentativa revalida o ator dentro da
/// transação e grava a auditoria administrativa; a verificação de commit ambíguo procura
/// essa entrada, como no <see cref="PrivateCatalogModerationStore"/>.
/// </summary>
internal sealed class TrainerAdministrationStore : ITrainerAdministrationStore
{
    private readonly PtManagerDbContext _dbContext;

    public TrainerAdministrationStore(PtManagerDbContext dbContext) =>
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    public async Task<TrainerAdministrationStoreStatus> SetBillingExemptionAsync(
        Guid actorUserId,
        Guid trainerId,
        bool isExempt,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var attempt = new MutationAttempt();
        try
        {
            return await ExecuteAsync(async token =>
            {
                if (!await IsActiveSuperuserAsync(actorUserId, token))
                    return TrainerAdministrationStoreStatus.ActorInvalid;

                var isTrainer = await _dbContext.Users
                    .IgnoreQueryFilters()
                    .AnyAsync(user => user.Id == trainerId && user.Role == "trainer" &&
                        !user.IsDeleted, token);
                if (!isTrainer)
                    return TrainerAdministrationStoreStatus.NotFound;

                var subscription = await _dbContext.TrainerSubscriptions
                    .FromSqlInterpolated(
                        $"SELECT * FROM trainer_subscriptions WHERE trainer_id = {trainerId} FOR UPDATE")
                    .IgnoreQueryFilters()
                    .SingleOrDefaultAsync(token);
                if (subscription is null)
                    return TrainerAdministrationStoreStatus.NotFound;
                if (subscription.IsExemptFromBilling == isExempt)
                    return TrainerAdministrationStoreStatus.Unchanged;

                var before = Snapshot(subscription);
                subscription.SetBillingExemption(isExempt, now);
                attempt.AuditEntry = AddAudit(
                    actorUserId,
                    isExempt ? "trainer_billing_exempted" : "trainer_billing_exemption_removed",
                    "trainer_subscription",
                    subscription.Id,
                    before,
                    Snapshot(subscription),
                    now);

                await _dbContext.SaveChangesAsync(token);
                return TrainerAdministrationStoreStatus.Changed;
            }, attempt, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return TrainerAdministrationStoreStatus.ConcurrencyConflict;
        }
    }

    public Task<TrainerAdministrationStoreStatus> BlockAccessAsync(
        Guid actorUserId,
        Guid trainerId,
        PlatformEnforcementReason reason,
        string? note,
        DateTime now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reason);

        var attempt = new MutationAttempt();
        return ExecuteAsync(async token =>
        {
            if (!await IsActiveSuperuserAsync(actorUserId, token))
                return TrainerAdministrationStoreStatus.ActorInvalid;
            if (!await LockTrainerAsync(trainerId, token))
                return TrainerAdministrationStoreStatus.NotFound;

            // Bloquear outra vez mantém o primeiro bloqueio: os 7 dias contam desde ele.
            if (await _dbContext.TrainerAccessBlocks.AnyAsync(block => block.TrainerId == trainerId, token))
                return TrainerAdministrationStoreStatus.Unchanged;

            var block = new TrainerAccessBlock(trainerId, reason, note, actorUserId, now);
            _dbContext.TrainerAccessBlocks.Add(block);

            // A sessão aberta do personal trainer acaba já; as dos clientes ficam nos 7 dias.
            await _dbContext.RefreshTokens
                .Where(item => item.UserId == trainerId && item.RevokedAt == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.RevokedAt, now), token);

            // O convite por aceitar deixa de servir e não volta quando o bloqueio acaba.
            await _dbContext.InviteTokens
                .Where(item => item.TrainerId == trainerId && item.UsedAt == null)
                .ExecuteDeleteAsync(token);

            attempt.AuditEntry = AddAudit(
                actorUserId,
                "trainer_access_blocked",
                "trainer",
                trainerId,
                null,
                Snapshot(block),
                now);
            await EnqueueAccessEmailsAsync(trainerId, blocked: true, attempt.AuditEntry.Id, now, token);

            await _dbContext.SaveChangesAsync(token);
            return TrainerAdministrationStoreStatus.Changed;
        }, attempt, cancellationToken);
    }

    public Task<TrainerAdministrationStoreStatus> UnblockAccessAsync(
        Guid actorUserId,
        Guid trainerId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var attempt = new MutationAttempt();
        return ExecuteAsync(async token =>
        {
            if (!await IsActiveSuperuserAsync(actorUserId, token))
                return TrainerAdministrationStoreStatus.ActorInvalid;
            if (!await LockTrainerAsync(trainerId, token))
                return TrainerAdministrationStoreStatus.NotFound;

            var block = await _dbContext.TrainerAccessBlocks
                .SingleOrDefaultAsync(item => item.TrainerId == trainerId, token);
            if (block is null)
                return TrainerAdministrationStoreStatus.Unchanged;

            var before = Snapshot(block);
            _dbContext.TrainerAccessBlocks.Remove(block);
            attempt.AuditEntry = AddAudit(
                actorUserId,
                "trainer_access_unblocked",
                "trainer",
                trainerId,
                before,
                JsonSerializer.Serialize(new { trainer_id = trainerId, blocked = false }),
                now);
            await EnqueueAccessEmailsAsync(trainerId, blocked: false, attempt.AuditEntry.Id, now, token);

            await _dbContext.SaveChangesAsync(token);
            return TrainerAdministrationStoreStatus.Changed;
        }, attempt, cancellationToken);
    }

    // Serializa bloquear e levantar sobre o mesmo personal trainer.
    private Task<bool> LockTrainerAsync(Guid trainerId, CancellationToken cancellationToken) =>
        _dbContext.Users
            .FromSqlInterpolated($"SELECT * FROM users WHERE id = {trainerId} FOR UPDATE")
            .IgnoreQueryFilters()
            .AnyAsync(user => user.Role == "trainer" && !user.IsDeleted, cancellationToken);

    /// <summary>
    /// Um email para o personal trainer e um por cliente ativo com conta ativa. Arquivados e
    /// quem nunca aceitou o convite não recebem (RN-013). O envio revalida tudo.
    /// </summary>
    private async Task EnqueueAccessEmailsAsync(
        Guid trainerId,
        bool blocked,
        Guid eventId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        _dbContext.OutboxMessages.Add(AccountAccessNotifications.Create(
            trainerId,
            blocked ? AccountAccessTemplateRenderer.TrainerBlockedKind : AccountAccessTemplateRenderer.TrainerRestoredKind,
            trainerId,
            eventId,
            now));

        var clientUserIds = await (
                from client in _dbContext.Clients.IgnoreQueryFilters().AsNoTracking()
                join user in _dbContext.Users.IgnoreQueryFilters().AsNoTracking()
                    on client.UserId equals (Guid?)user.Id
                where client.OwnerTrainerId == trainerId && client.IsActive && !client.IsDeleted &&
                    user.Role == "client" && user.IsActive && !user.IsDeleted
                select user.Id)
            .ToListAsync(cancellationToken);

        foreach (var userId in clientUserIds)
            _dbContext.OutboxMessages.Add(AccountAccessNotifications.Create(
                trainerId,
                blocked ? AccountAccessTemplateRenderer.ClientBlockedKind : AccountAccessTemplateRenderer.ClientRestoredKind,
                userId,
                eventId,
                now));
    }

    public async Task<IssueTrainerInvitationStoreResult> IssueInvitationAsync(
        Guid actorUserId,
        string email,
        string tokenHash,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var address = new EmailAddress(email);
        var attempt = new MutationAttempt();
        TrainerInvitation? issued = null;

        try
        {
            var status = await ExecuteAsync(async token =>
            {
                issued = null;
                if (!await IsActiveSuperuserAsync(actorUserId, token))
                    return TrainerAdministrationStoreStatus.ActorInvalid;

                // Qualquer conta com este email, de qualquer papel, recusa o convite: o superuser
                // isenta a conta existente em vez de criar outra (RN-008).
                if (await _dbContext.Users.IgnoreQueryFilters().AnyAsync(
                    user => user.NormalizedEmail == address.Normalized, token))
                    return TrainerAdministrationStoreStatus.EmailAlreadyRegistered;

                var previous = await _dbContext.TrainerInvitations
                    .FromSqlInterpolated($"""
                        SELECT * FROM trainer_invitations
                        WHERE normalized_email = {address.Normalized}
                            AND used_at IS NULL AND revoked_at IS NULL
                        FOR UPDATE
                        """)
                    .ToListAsync(token);
                foreach (var invitation in previous)
                    invitation.Revoke(now);
                // O índice único parcial só aceita o novo pendente depois de o anterior sair.
                if (previous.Count > 0)
                    await _dbContext.SaveChangesAsync(token);

                issued = new TrainerInvitation(address, tokenHash, actorUserId, now);
                _dbContext.TrainerInvitations.Add(issued);
                attempt.AuditEntry = AddAudit(
                    actorUserId,
                    "trainer_invitation_sent",
                    "trainer_invitation",
                    issued.Id,
                    previous.Count == 0
                        ? null
                        : JsonSerializer.Serialize(new { revoked_invitation_ids = previous.Select(item => item.Id) }),
                    Snapshot(issued),
                    now);

                await _dbContext.SaveChangesAsync(token);
                return TrainerAdministrationStoreStatus.Changed;
            }, attempt, cancellationToken);

            return new IssueTrainerInvitationStoreResult(
                status,
                status == TrainerAdministrationStoreStatus.Changed && issued is not null
                    ? new PendingTrainerInvitationDto(issued.Id, issued.Email, issued.CreatedAt, issued.ExpiresAt)
                    : null);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "uq_trainer_invitations_pending_email"
        })
        {
            return new IssueTrainerInvitationStoreResult(
                TrainerAdministrationStoreStatus.ConcurrencyConflict,
                null);
        }
    }

    public Task<TrainerAdministrationStoreStatus> RevokeInvitationAsync(
        Guid actorUserId,
        Guid invitationId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var attempt = new MutationAttempt();
        return ExecuteAsync(async token =>
        {
            if (!await IsActiveSuperuserAsync(actorUserId, token))
                return TrainerAdministrationStoreStatus.ActorInvalid;

            var invitation = await _dbContext.TrainerInvitations
                .FromSqlInterpolated(
                    $"SELECT * FROM trainer_invitations WHERE id = {invitationId} FOR UPDATE")
                .SingleOrDefaultAsync(token);
            if (invitation is null || invitation.IsUsed)
                return TrainerAdministrationStoreStatus.NotFound;

            var before = Snapshot(invitation);
            if (!invitation.Revoke(now))
                return TrainerAdministrationStoreStatus.Unchanged;

            attempt.AuditEntry = AddAudit(
                actorUserId,
                "trainer_invitation_revoked",
                "trainer_invitation",
                invitation.Id,
                before,
                Snapshot(invitation),
                now);

            await _dbContext.SaveChangesAsync(token);
            return TrainerAdministrationStoreStatus.Changed;
        }, attempt, cancellationToken);
    }

    private Task<bool> IsActiveSuperuserAsync(Guid actorUserId, CancellationToken cancellationToken) =>
        _dbContext.Users
            .FromSqlInterpolated($"""
                SELECT * FROM users WHERE id = {actorUserId} FOR SHARE
            """)
            .IgnoreQueryFilters()
            .AnyAsync(user =>
                user.Role == "superuser" && user.IsActive && !user.IsDeleted, cancellationToken);

    private AdministrativeAuditEntry AddAudit(
        Guid actorUserId,
        string action,
        string resourceType,
        Guid resourceId,
        string? before,
        string after,
        DateTime now)
    {
        var entry = new AdministrativeAuditEntry(
            actorUserId, action, resourceType, resourceId, before, after, now);
        _dbContext.AdministrativeAuditEntries.Add(entry);
        return entry;
    }

    private static string Snapshot(TrainerSubscription subscription) => JsonSerializer.Serialize(new
    {
        trainer_id = subscription.TrainerId,
        status = subscription.Status.Value,
        tier = subscription.Tier.Value,
        is_exempt_from_billing = subscription.IsExemptFromBilling,
        billing_exempt_since = subscription.BillingExemptSince
    });

    private static string Snapshot(TrainerAccessBlock block) => JsonSerializer.Serialize(new
    {
        trainer_id = block.TrainerId,
        reason = block.Reason.Value,
        note = block.Note,
        blocked_at = block.BlockedAt
    });

    // O hash do token nunca entra na auditoria.
    private static string Snapshot(TrainerInvitation invitation) => JsonSerializer.Serialize(new
    {
        email = invitation.Email,
        expires_at = invitation.ExpiresAt,
        revoked_at = invitation.RevokedAt
    });

    private Task<TrainerAdministrationStoreStatus> ExecuteAsync(
        Func<CancellationToken, Task<TrainerAdministrationStoreStatus>> operation,
        MutationAttempt attempt,
        CancellationToken cancellationToken)
    {
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return strategy.ExecuteInTransactionAsync(
            async operationToken =>
            {
                _dbContext.ChangeTracker.Clear();
                attempt.AuditEntry = null;
                return await operation(operationToken);
            },
            verificationToken => attempt.AuditEntry is null
                ? Task.FromResult(false)
                : _dbContext.AdministrativeAuditEntries
                    .AsNoTracking()
                    .AnyAsync(entry => entry.Id == attempt.AuditEntry!.Id, verificationToken),
            IsolationLevel.ReadCommitted,
            cancellationToken);
    }

    private sealed class MutationAttempt
    {
        public AdministrativeAuditEntry? AuditEntry { get; set; }
    }
}
