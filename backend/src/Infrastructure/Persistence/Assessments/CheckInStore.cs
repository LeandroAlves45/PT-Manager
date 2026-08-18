using System.Data;
using Application.Common.Abstractions;
using Application.Features.Assessments.CheckIns.Abstractions;
using Domain.Entities.Assessments;
using Domain.Entities.Clients;
using Domain.ValueObjects;
using Infrastructure.Data;
using Infrastructure.Persistence.Errors;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Assessments;

/// <summary>Persiste check-ins e serializa agendamento, resposta e correção.</summary>
internal sealed class CheckInStore : ICheckInStore
{
    private readonly PtManagerDbContext _dbContext;
    private readonly ITrainerTimeZoneProvider _timeZoneProvider;
    private readonly PostgresConstraintTranslator _constraintTranslator;

    public CheckInStore(
        PtManagerDbContext dbContext,
        ITrainerTimeZoneProvider timeZoneProvider,
        PostgresConstraintTranslator constraintTranslator
    )
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(timeZoneProvider);
        ArgumentNullException.ThrowIfNull(constraintTranslator);
        _dbContext = dbContext;
        _timeZoneProvider = timeZoneProvider;
        _constraintTranslator = constraintTranslator;
    }

    public Task<CheckInStoreResult> CreateAsync(
        Guid trainerId,
        Guid clientId,
        DateOnly checkInDate,
        DateOnly? targetDate,
        DateTime now,
        CancellationToken cancellationToken) =>
        ExecuteTransactionAsync(
            () => CreateOnceAsync(
                trainerId,
                clientId,
                checkInDate,
                targetDate,
                now,
                cancellationToken),
            cancellationToken
        );

    public Task<CheckInStoreResult> RescheduleAsync(
        Guid trainerId,
        Guid checkInId,
        DateOnly checkInDate,
        DateOnly? targetDate,
        DateTime now,
        CancellationToken cancellationToken) =>
        ExecuteTransactionAsync(
            () => RescheduleOnceAsync(
                trainerId,
                checkInId,
                checkInDate,
                targetDate,
                now,
                cancellationToken),
            cancellationToken
        );

    public Task<CheckInStoreResult> CancelAsync(
        Guid trainerId,
        Guid checkInId,
        DateTime now,
        CancellationToken cancellationToken) =>
        ExecuteTransactionAsync(
            () => CancelOnceAsync(
                trainerId,
                checkInId,
                now,
                cancellationToken),
            cancellationToken
        );

    public Task<CheckInStoreResult> SubmitResponseAsync(
        Guid trainerId,
        Guid userId,
        Guid checkInId,
        decimal weightKg,
        decimal? bodyFatPercentage,
        string? notes,
        BodyMeasurements bodyMeasurements,
        CheckInFeedback feedback,
        int? trainingAdherenceScore,
        int? nutritionAdherenceScore,
        DateTime now,
        CancellationToken cancellationToken) =>
        ExecuteTransactionAsync(
            () => SubmitResponseOnceAsync(
                trainerId,
                userId,
                checkInId,
                weightKg,
                bodyFatPercentage,
                notes,
                bodyMeasurements,
                feedback,
                trainingAdherenceScore,
                nutritionAdherenceScore,
                now,
                cancellationToken),
            cancellationToken
        );

    public Task<CheckInStoreResult> CorrectAsync(
        Guid trainerId,
        Guid checkInId,
        DateOnly? targetDate,
        decimal weightKg,
        decimal? bodyFatPercentage,
        string? notes,
        BodyMeasurements bodyMeasurements,
        CheckInFeedback feedback,
        int? trainingAdherenceScore,
        int? nutritionAdherenceScore,
        DateTime now,
        CancellationToken cancellationToken) =>
        ExecuteTransactionAsync(
            () => CorrectOnceAsync(
                trainerId,
                checkInId,
                targetDate,
                weightKg,
                bodyFatPercentage,
                notes,
                bodyMeasurements,
                feedback,
                trainingAdherenceScore,
                nutritionAdherenceScore,
                now,
                cancellationToken),
            cancellationToken
        );

    private async Task<CheckInStoreResult> CreateOnceAsync(
        Guid trainerId,
        Guid clientId,
        DateOnly checkInDate,
        DateOnly? targetDate,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var localToday = await GetLocalTodayAsync(trainerId, now, cancellationToken);
        if (checkInDate < localToday)
            return CheckInStoreResult.For(CheckInStoreResult.Status.DateNotAllowed);

        var client = await LockClientAsync(trainerId, clientId, cancellationToken);
        if (client is null)
            return CheckInStoreResult.For(CheckInStoreResult.Status.ClientNotFound);
        if (!client.IsActive)
            return CheckInStoreResult.For(CheckInStoreResult.Status.ClientInactive);

        var exists = await _dbContext.CheckIns
            .AnyAsync(checkIn => checkIn.OwnerTrainerId == trainerId
                && checkIn.ClientId == clientId
                && checkIn.CheckInDate == checkInDate,
                cancellationToken);
        if (exists)
            return CheckInStoreResult.For(CheckInStoreResult.Status.DateConflict);

        var checkIn = new CheckIn(
            trainerId,
            clientId,
            checkInDate,
            targetDate,
            now
        );
        _dbContext.CheckIns.Add(checkIn);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDateConflict(
            exception, PersistenceOperation.CreateCheckIn))
        {
            return CheckInStoreResult.For(CheckInStoreResult.Status.DateConflict);
        }

        return CheckInStoreResult.For(CheckInStoreResult.Status.Created, checkIn);
    }

    private async Task<CheckInStoreResult> RescheduleOnceAsync(
        Guid trainerId,
        Guid checkInId,
        DateOnly checkInDate,
        DateOnly? targetDate,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var localToday = await GetLocalTodayAsync(trainerId, now, cancellationToken);
        var locked = await LockClientAndCheckInAsync(trainerId, checkInId, cancellationToken);
        if (locked is null)
            return CheckInStoreResult.For(CheckInStoreResult.Status.CheckInNotFound);

        var checkIn = locked.Value.CheckIn;
        if (checkIn.CancelledAt.HasValue || checkIn.RespondedAt.HasValue ||
            checkIn.CheckInDate <= localToday || checkInDate <= localToday)
            return CheckInStoreResult.For(CheckInStoreResult.Status.CannotReschedule);
        if (checkInDate == checkIn.CheckInDate && targetDate == checkIn.TargetDate)
            return CheckInStoreResult.For(
                CheckInStoreResult.Status.AlreadyInRequestedState, checkIn);

        checkIn.Reschedule(checkInDate, targetDate, localToday, now);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDateConflict(
            exception, PersistenceOperation.RescheduleCheckIn))
        {
            return CheckInStoreResult.For(CheckInStoreResult.Status.DateConflict);
        }

        return CheckInStoreResult.For(CheckInStoreResult.Status.Rescheduled, checkIn);
    }

    private async Task<CheckInStoreResult> CancelOnceAsync(
        Guid trainerId,
        Guid checkInId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var localToday = await GetLocalTodayAsync(trainerId, now, cancellationToken);
        var locked = await LockClientAndCheckInAsync(trainerId, checkInId, cancellationToken);
        if (locked is null)
            return CheckInStoreResult.For(CheckInStoreResult.Status.CheckInNotFound);

        var checkIn = locked.Value.CheckIn;
        if (checkIn.CancelledAt.HasValue)
            return CheckInStoreResult.For(
                CheckInStoreResult.Status.AlreadyInRequestedState, checkIn);
        if (checkIn.RespondedAt.HasValue || checkIn.CheckInDate <= localToday)
            return CheckInStoreResult.For(CheckInStoreResult.Status.CannotCancel);

        checkIn.Cancel(localToday, now);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return CheckInStoreResult.For(CheckInStoreResult.Status.Cancelled, checkIn);
    }

    private async Task<CheckInStoreResult> SubmitResponseOnceAsync(
        Guid trainerId,
        Guid userId,
        Guid checkInId,
        decimal weightKg,
        decimal? bodyFatPercentage,
        string? notes,
        BodyMeasurements bodyMeasurements,
        CheckInFeedback feedback,
        int? trainingAdherenceScore,
        int? nutritionAdherenceScore,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var localToday = await GetLocalTodayAsync(trainerId, now, cancellationToken);
        var client = await LockClientByUserAsync(trainerId, userId, cancellationToken);
        if (client is null)
            return CheckInStoreResult.For(CheckInStoreResult.Status.CheckInNotFound);
        if (!client.IsActive)
            return CheckInStoreResult.For(CheckInStoreResult.Status.ClientInactive);

        var checkIn = await LockCheckInAsync(trainerId, checkInId, cancellationToken);
        if (checkIn is null || checkIn.ClientId != client.Id)
            return CheckInStoreResult.For(CheckInStoreResult.Status.CheckInNotFound);
        if (checkIn.CancelledAt.HasValue)
            return CheckInStoreResult.For(CheckInStoreResult.Status.CheckInCancelled);
        if (checkIn.RespondedAt.HasValue)
            return checkIn.MatchesResponse(
                weightKg,
                bodyFatPercentage,
                notes,
                bodyMeasurements,
                feedback,
                trainingAdherenceScore,
                nutritionAdherenceScore
            )
                ? CheckInStoreResult.For(
                    CheckInStoreResult.Status.AlreadyInRequestedState, checkIn)
                : CheckInStoreResult.For(CheckInStoreResult.Status.AlreadyAnswered);

        if (checkIn.CheckInDate != localToday)
            return CheckInStoreResult.For(CheckInStoreResult.Status.WrongResponseDay);

        checkIn.SubmitResponse(
            weightKg,
            bodyFatPercentage,
            notes,
            bodyMeasurements,
            feedback,
            trainingAdherenceScore,
            nutritionAdherenceScore,
            localToday,
            now
        );
        await _dbContext.SaveChangesAsync(cancellationToken);
        return CheckInStoreResult.For(CheckInStoreResult.Status.Answered, checkIn);
    }

    private async Task<CheckInStoreResult> CorrectOnceAsync(
        Guid trainerId,
        Guid checkInId,
        DateOnly? targetDate,
        decimal weightKg,
        decimal? bodyFatPercentage,
        string? notes,
        BodyMeasurements bodyMeasurements,
        CheckInFeedback feedback,
        int? trainingAdherenceScore,
        int? nutritionAdherenceScore,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var locked = await LockClientAndCheckInAsync(trainerId, checkInId, cancellationToken);
        if (locked is null)
            return CheckInStoreResult.For(CheckInStoreResult.Status.CheckInNotFound);

        var checkIn = locked.Value.CheckIn;
        if (checkIn.CancelledAt.HasValue)
            return CheckInStoreResult.For(CheckInStoreResult.Status.CheckInCancelled);
        if (!checkIn.RespondedAt.HasValue)
            return CheckInStoreResult.For(CheckInStoreResult.Status.NotAnswered);
        if (targetDate.HasValue && targetDate.Value < checkIn.CheckInDate)
            return CheckInStoreResult.For(CheckInStoreResult.Status.DateNotAllowed);

        if (checkIn.TargetDate == targetDate && checkIn.MatchesResponse(
            weightKg,
            bodyFatPercentage,
            notes,
            bodyMeasurements,
            feedback,
            trainingAdherenceScore,
            nutritionAdherenceScore))
            return CheckInStoreResult.For(
                CheckInStoreResult.Status.AlreadyInRequestedState, checkIn);

        checkIn.Correct(
            targetDate,
            weightKg,
            bodyFatPercentage,
            notes,
            bodyMeasurements,
            feedback,
            trainingAdherenceScore,
            nutritionAdherenceScore,
            now
        );
        await _dbContext.SaveChangesAsync(cancellationToken);
        return CheckInStoreResult.For(CheckInStoreResult.Status.Corrected, checkIn);
    }

    private async Task<DateOnly> GetLocalTodayAsync(
        Guid trainerId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var timeZone = await _timeZoneProvider.GetRequiredAsync(trainerId, cancellationToken);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(now, timeZone));
    }

    private Task<Client?> LockClientAsync(
        Guid trainerId,
        Guid clientId,
        CancellationToken cancellationToken) =>
        _dbContext.Clients
            .FromSqlInterpolated($"""
                SELECT *
                FROM clients
                WHERE owner_trainer_id = {trainerId}
                    AND id = {clientId}
                    AND is_deleted = false
                FOR UPDATE
                """)
            .SingleOrDefaultAsync(cancellationToken);

    private Task<Client?> LockClientByUserAsync(
        Guid trainerId,
        Guid userId,
        CancellationToken cancellationToken) =>
        _dbContext.Clients
            .FromSqlInterpolated($"""
                SELECT *
                FROM clients
                WHERE owner_trainer_id = {trainerId}
                    AND user_id = {userId}
                    AND is_deleted = false
                FOR UPDATE
                """)
            .SingleOrDefaultAsync(cancellationToken);

    private async Task<(Client Client, CheckIn CheckIn)?> LockClientAndCheckInAsync(
        Guid trainerId,
        Guid checkInId,
        CancellationToken cancellationToken)
    {
        var clientId = await _dbContext.CheckIns
            .AsNoTracking()
            .Where(checkIn => checkIn.OwnerTrainerId == trainerId
                && checkIn.Id == checkInId)
            .Select(checkIn => (Guid?)checkIn.ClientId)
            .SingleOrDefaultAsync(cancellationToken);
        if (!clientId.HasValue)
            return null;

        var client = await LockClientAsync(trainerId, clientId.Value, cancellationToken);
        if (client is null)
            return null;

        var checkIn = await LockCheckInAsync(trainerId, checkInId, cancellationToken);
        return checkIn is null || checkIn.ClientId != client.Id
            ? null
            : (client, checkIn);
    }

    private Task<CheckIn?> LockCheckInAsync(
        Guid trainerId,
        Guid checkInId,
        CancellationToken cancellationToken) =>
        _dbContext.CheckIns
            .FromSqlInterpolated($"""
                SELECT *
                FROM checkins
                WHERE owner_trainer_id = {trainerId}
                    AND id = {checkInId}
                    AND is_deleted = false
                FOR UPDATE
                """)
            .SingleOrDefaultAsync(cancellationToken);

    private bool IsDateConflict(
        DbUpdateException exception,
        PersistenceOperation operation) =>
        _constraintTranslator.TryTranslate(exception, operation, out var error)
            && error?.Code == "check_in_date_conflict";

    private async Task<CheckInStoreResult> ExecuteTransactionAsync(
        Func<Task<CheckInStoreResult>> operation,
        CancellationToken cancellationToken)
    {
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dbContext.Database
                .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
            var result = await operation();

            if (result.Kind is CheckInStoreResult.Status.Created or
                CheckInStoreResult.Status.Rescheduled or
                CheckInStoreResult.Status.Cancelled or
                CheckInStoreResult.Status.Answered or
                CheckInStoreResult.Status.Corrected or
                CheckInStoreResult.Status.AlreadyInRequestedState)
                await transaction.CommitAsync(cancellationToken);
            else
                await transaction.RollbackAsync(cancellationToken);

            return result;
        });
    }
}

