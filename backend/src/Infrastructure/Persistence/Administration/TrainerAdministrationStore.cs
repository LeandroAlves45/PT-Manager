using System.Data;
using System.Text.Json;
using Application.Features.Administration.Trainers.Abstractions;
using Application.Features.Administration.Trainers.Dtos;
using Domain.Entities.Administration;
using Domain.Entities.Billing;
using Domain.Entities.Identity;
using Domain.ValueObjects;
using Infrastructure.Data;
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
