using Application.Features.TrainerSettings.Abstractions;
using Domain.Entities.Jobs;
using Domain.Entities.TrainerSettings;
using Infrastructure.Data;
using Infrastructure.Persistence.Common;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.TrainerSettings;

/// <summary>Persiste mutações de definições do personal trainer de forma transacional.</summary>
internal sealed class TrainerSettingsStore : ITrainerSettingsStore
{
    private readonly PtManagerDbContext _dbContext;

    public TrainerSettingsStore(PtManagerDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public Task<TrainerSettingsStoreResult> UpdateBrandingAsync(
        Guid trainerId,
        string appName,
        string? primaryColor,
        string? bodyColor,
        DateTime now,
        CancellationToken cancellationToken) => ExecuteAsync(async () =>
        {
            var settings = await LoadTrackedRequiredAsync(trainerId, cancellationToken);
            settings.UpdateBranding(appName, primaryColor, bodyColor, now);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return TrainerSettingsStoreResult.Updated(settings);
        });

    public Task<TrainerSettingsStoreResult> ResetBrandingColorsAsync(
        Guid trainerId,
        DateTime now,
        CancellationToken cancellationToken) => ExecuteAsync(async () =>
        {
            var settings = await LoadTrackedRequiredAsync(trainerId, cancellationToken);
            settings.ResetColors(now);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return TrainerSettingsStoreResult.Updated(settings);
        });

    public Task<TrainerSettingsStoreResult> UpdateContactsAsync(
        Guid trainerId,
        string? phone,
        string? address,
        string? city,
        DateTime now,
        CancellationToken cancellationToken) => ExecuteAsync(async () =>
        {
            var settings = await LoadTrackedRequiredAsync(trainerId, cancellationToken);
            settings.UpdateContacts(phone, address, city, now);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return TrainerSettingsStoreResult.Updated(settings);
        });

    public Task<TrainerSettingsStoreResult> ChangeTimezoneAsync(
        Guid trainerId,
        string timezone,
        DateTime now,
        CancellationToken cancellationToken) => ExecuteInTransactionAsync(async transaction =>
        {
            await _dbContext.LockTrainerAsync(trainerId, cancellationToken);
            var settings = await LoadTrackedRequiredAsync(trainerId, cancellationToken);

            // Idempotente: mesmo timezone não verifica conflitos nem escreve.
            if (string.Equals(settings.Timezone, timezone, StringComparison.Ordinal))
            {
                await transaction.CommitAsync(cancellationToken);
                return TrainerSettingsStoreResult.Updated(settings);
            }

            var newZone = TimeZoneInfo.FindSystemTimeZoneById(timezone);
            if (await HasFutureScheduleConflictAsync(trainerId, newZone, now, cancellationToken))
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return TrainerSettingsStoreResult.Conflict();
            }

            settings.ChangeTimezone(timezone, now);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return TrainerSettingsStoreResult.Updated(settings);
        }, cancellationToken);

    public Task<TrainerSettingsStoreResult> ReplaceLogoAsync(
        Guid trainerId,
        string logoUrl,
        string logoPublicId,
        Guid correlationId,
        DateTime now,
        CancellationToken cancellationToken) => ExecuteInTransactionAsync(async transaction =>
        {
            await _dbContext.LockTrainerAsync(trainerId, cancellationToken);
            var settings = await LoadTrackedRequiredAsync(trainerId, cancellationToken);

            // Uma repetição da mesma correlação após uma falha transitória não pode
            // agendar a eliminação do asset que continua ativo.
            if (string.Equals(settings.LogoUrl, logoUrl, StringComparison.Ordinal) &&
                string.Equals(settings.LogoPublicId, logoPublicId, StringComparison.Ordinal))
            {
                await transaction.CommitAsync(cancellationToken);
                return TrainerSettingsStoreResult.Updated(settings);
            }

            var previousPublicId = settings.ReplaceLogo(logoUrl, logoPublicId, now);

            if (previousPublicId is not null)
                EnqueueLogoDeletion(trainerId, previousPublicId, correlationId, now);

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return TrainerSettingsStoreResult.Updated(settings, previousPublicId);
        }, cancellationToken);

    public Task<TrainerSettingsStoreResult> RemoveLogoAsync(
        Guid trainerId,
        Guid correlationId,
        DateTime now,
        CancellationToken cancellationToken) => ExecuteInTransactionAsync(async transaction =>
        {
            await _dbContext.LockTrainerAsync(trainerId, cancellationToken);
            var settings = await LoadTrackedRequiredAsync(trainerId, cancellationToken);

            var previousPublicId = settings.RemoveLogo(now);
            if (previousPublicId is not null)
                EnqueueLogoDeletion(trainerId, previousPublicId, correlationId, now);

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return TrainerSettingsStoreResult.Updated(settings, previousPublicId);
        }, cancellationToken);

    /// <summary>
    /// Agenda, na mesma transação, a eliminação do asset anterior. A chave de
    /// idempotência deriva da correlação da mutação e mantém tamanho fixo,
    /// independentemente do public_id fornecido pelo storage.
    /// </summary>
    private void EnqueueLogoDeletion(
        Guid trainerId,
        string previousPublicId,
        Guid correlationId,
        DateTime now)
    {
        var payload = System.Text.Json.JsonSerializer.Serialize(new { public_id = previousPublicId });
        var message = new OutboxMessage(
            trainerId,
            "trainer-logo.delete",
            payload,
            $"trainer-logo.delete.{correlationId:N}",
            correlationId,
            now);
        _dbContext.OutboxMessages.Add(message);
    }

    /// <summary>
    /// Uma única query: agrupa as sessões futuras Scheduled do personal trainer pelo
    /// par (cliente, dia local sob o NOVO timezone) e existe conflito se
    /// algum grupo tiver mais de uma sessão.
    /// </summary>
    private Task<bool> HasFutureScheduleConflictAsync(
        Guid trainerId,
        TimeZoneInfo newZone,
        DateTime now,
        CancellationToken cancellationToken) =>
        _dbContext.Database.SqlQuery<bool>($"""
            SELECT EXISTS (
                SELECT 1
                FROM sessions
                WHERE owner_trainer_id = {trainerId}
                    AND status = 'scheduled'
                    AND is_deleted = false
                    AND starts_at > {now}
                GROUP BY client_id, (starts_at AT TIME ZONE {newZone.Id})::date
                HAVING COUNT(*) > 1
            ) AS "Value"
            """).SingleOrDefaultAsync(cancellationToken);

    private async Task<Domain.Entities.TrainerSettings.TrainerSettings> LoadTrackedRequiredAsync(
        Guid trainerId,
        CancellationToken cancellationToken) =>
        await _dbContext.TrainerSettings
            .SingleOrDefaultAsync(settings => settings.TrainerId == trainerId, cancellationToken)
            ?? throw new InvalidOperationException(
                "TrainerSettings must exist for every personal trainer since onboarding.");

    private Task<TrainerSettingsStoreResult> ExecuteAsync(
        Func<Task<TrainerSettingsStoreResult>> operation)
    {
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(operation);
    }

    private Task<TrainerSettingsStoreResult> ExecuteInTransactionAsync(
        Func<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction,
            Task<TrainerSettingsStoreResult>> operation,
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


