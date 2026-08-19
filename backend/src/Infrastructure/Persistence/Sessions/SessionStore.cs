using Application.Common.Abstractions;
using Application.Features.Sessions.Abstractions;
using Domain.Entities.Billing;
using Domain.Entities.Clients;
using Domain.Entities.Sessions;
using Domain.ValueObjects;
using Infrastructure.Data;
using Infrastructure.Persistence.Errors;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Sessions;

/// <summary>Persiste sessões com serialização da agenda e do saldo de packs.</summary>
internal sealed class SessionStore : ISessionStore
{
    private readonly PtManagerDbContext _dbContext;
    private readonly ITrainerTimeZoneProvider _timeZoneProvider;
    private readonly PostgresConstraintTranslator _constraintTranslator;

    public SessionStore(
        PtManagerDbContext dbContext,
        ITrainerTimeZoneProvider timeZoneProvider,
        PostgresConstraintTranslator constraintTranslator)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _timeZoneProvider = timeZoneProvider ?? throw new ArgumentNullException(nameof(timeZoneProvider));
        _constraintTranslator = constraintTranslator ?? throw new ArgumentNullException(nameof(constraintTranslator));
    }

    public Task<SessionStoreResult> CreateAsync(
        Guid trainerId,
        Guid clientId,
        Guid? packId,
        DateTimeOffset startsAt,
        int durationMinutes,
        string? location,
        string? sessionType,
        string? notes,
        DateTime now,
        CancellationToken cancellationToken) =>
        ExecuteTransactionAsync(
            () => CreateOnceAsync(
                trainerId,
                clientId,
                packId,
                startsAt.ToUniversalTime(),
                durationMinutes,
                location,
                sessionType,
                notes,
                now,
                cancellationToken
            ),
            cancellationToken
        );

    public Task<SessionStoreResult> RescheduleAsync(
        Guid trainerId,
        Guid sessionId,
        DateTimeOffset startsAt,
        int durationMinutes,
        string? location,
        DateTime now,
        CancellationToken cancellationToken) =>
        ExecuteTransactionAsync(
            () => RescheduleOnceAsync(
                trainerId,
                sessionId,
                startsAt.ToUniversalTime(),
                durationMinutes,
                location,
                now,
                cancellationToken
            ),
            cancellationToken
        );

    public Task<SessionStoreResult> ChangePackAsync(
        Guid trainerId,
        Guid sessionId,
        Guid? packId,
        DateTime now,
        CancellationToken cancellationToken) =>
        ExecuteTransactionAsync(
            () => ChangePackOnceAsync(
                trainerId,
                sessionId,
                packId,
                now,
                cancellationToken
            ),
            cancellationToken
        );

    public Task<SessionStoreResult> TransitionAsync(
        Guid trainerId,
        Guid sessionId,
        SessionTransition transition,
        DateTime now,
        CancellationToken cancellationToken) =>
        ExecuteTransactionAsync(
            () => TransitionOnceAsync(
                trainerId,
                sessionId,
                transition,
                now,
                cancellationToken
            ),
            cancellationToken
        );

    private async Task<SessionStoreResult> CreateOnceAsync(
        Guid trainerId,
        Guid clientId,
        Guid? packId,
        DateTimeOffset startsAt,
        int durationMinutes,
        string? location,
        string? sessionType,
        string? notes,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await LockTrainerAsync(trainerId, cancellationToken);
        if (startsAt <= AsOffset(now))
            return SessionStoreResult.For(SessionStoreResult.Status.StartsAtNotFuture);

        var client = await LockClientAsync(trainerId, clientId, cancellationToken);
        if (client is null)
            return SessionStoreResult.For(SessionStoreResult.Status.ClientNotFound);
        if (!client.IsActive)
            return SessionStoreResult.For(SessionStoreResult.Status.ClientInactive);

        if (packId.HasValue && await LockUsablePackAsync(
            trainerId,
            clientId,
            packId.Value,
            cancellationToken) is null)
            return SessionStoreResult.For(SessionStoreResult.Status.PackNotAvailable);

        var scheduleFailure = await ValidateScheduleAsync(
            trainerId,
            clientId,
            startsAt,
            durationMinutes,
            null,
            cancellationToken
        );
        if (scheduleFailure is not null)
            return scheduleFailure;

        var session = new Session(
            trainerId,
            clientId,
            packId,
            startsAt,
            durationMinutes,
            location,
            sessionType,
            notes,
            now
        );
        _dbContext.Sessions.Add(session);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (IsScheduleConflict(exception, PersistenceOperation.CreateSession))
        {
            return SessionStoreResult.For(SessionStoreResult.Status.TrainerScheduleConflict);
        }
        return SessionStoreResult.ForCreated(session);
    }

    private async Task<SessionStoreResult> RescheduleOnceAsync(
        Guid trainerId,
        Guid sessionId,
        DateTimeOffset startsAt,
        int durationMinutes,
        string? location,
        DateTime now,
        CancellationToken cancellationToken)
    {
        // Personal trainer primeiro: esta é a mesma ordem usada por Create e Restore futuro.
        await LockTrainerAsync(trainerId, cancellationToken);
        if (startsAt <= AsOffset(now))
            return SessionStoreResult.For(SessionStoreResult.Status.StartsAtNotFuture);

        var session = await LockSessionAsync(trainerId, sessionId, cancellationToken);
        if (session is null)
            return SessionStoreResult.For(SessionStoreResult.Status.SessionNotFound);
        if (session.Status != SessionStatus.Scheduled)
            return SessionStoreResult.For(SessionStoreResult.Status.InvalidState);

        var normalizedLocation = NormalizeOptional(location);
        if (session.StartsAt == startsAt &&
            session.DurationMinutes == durationMinutes &&
            session.Location == normalizedLocation)
            return SessionStoreResult.ForAlreadyRequested(session);

        var scheduleFailure = await ValidateScheduleAsync(
            trainerId,
            session.ClientId,
            startsAt,
            durationMinutes,
            session.Id,
            cancellationToken
        );
        if (scheduleFailure is not null)
            return scheduleFailure;

        session.Reschedule(startsAt, durationMinutes, location, now);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (IsScheduleConflict(exception, PersistenceOperation.RescheduleSession))
        {
            return SessionStoreResult.For(SessionStoreResult.Status.TrainerScheduleConflict);
        }

        return SessionStoreResult.ForUpdated(session);
    }

    private async Task<SessionStoreResult> ChangePackOnceAsync(
        Guid trainerId,
        Guid sessionId,
        Guid? packId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var session = await LockSessionAsync(trainerId, sessionId, cancellationToken);
        if (session is null)
            return SessionStoreResult.For(SessionStoreResult.Status.SessionNotFound);
        if (session.Status != SessionStatus.Scheduled)
            return SessionStoreResult.For(SessionStoreResult.Status.InvalidState);
        if (session.ClientSessionPackId == packId)
            return SessionStoreResult.ForAlreadyRequested(session);

        if (packId.HasValue && await LockUsablePackAsync(
            trainerId,
            session.ClientId,
            packId.Value,
            cancellationToken) is null)
            return SessionStoreResult.For(SessionStoreResult.Status.PackNotAvailable);

        session.ChangePack(packId, now);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return SessionStoreResult.ForUpdated(session);
    }

    private async Task<SessionStoreResult> TransitionOnceAsync(
        Guid trainerId,
        Guid sessionId,
        SessionTransition transition,
        DateTime now,
        CancellationToken cancellationToken)
    {
        // Restore pode voltar a ocupar agenda futura, por isso participa no lock do personal trainer.
        if (transition == SessionTransition.Restore)
            await LockTrainerAsync(trainerId, cancellationToken);

        var session = await LockSessionAsync(trainerId, sessionId, cancellationToken);
        if (session is null)
            return SessionStoreResult.For(SessionStoreResult.Status.SessionNotFound);

        var targetStatus = TargetStatus(transition);
        if (session.Status == targetStatus)
            return SessionStoreResult.ForAlreadyRequested(session);

        if (transition != SessionTransition.Restore &&
            session.Status != SessionStatus.Scheduled)
            return SessionStoreResult.For(SessionStoreResult.Status.InvalidState);

        if ((transition is SessionTransition.Complete or SessionTransition.MarkNoShow) &&
            AsOffset(now) < session.StartsAt)
            return SessionStoreResult.For(SessionStoreResult.Status.TransitionTooEarly);

        if (transition == SessionTransition.Restore)
            return await RestoreAsync(session, trainerId, now, cancellationToken);

        if ((transition is SessionTransition.Complete or SessionTransition.MarkNoShow) &&
            session.ClientSessionPackId.HasValue)
        {
            var pack = await LockPackAsync(
                trainerId,
                session.ClientId,
                session.ClientSessionPackId.Value,
                cancellationToken
            );
            if (pack is null)
                return SessionStoreResult.For(SessionStoreResult.Status.PackNotAvailable);
            if (!pack.IsUsable)
                return SessionStoreResult.For(SessionStoreResult.Status.PackBalanceUnavailable);

            pack.ConsumeSession(now);
        }

        ApplyTransition(session, transition, now);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return SessionStoreResult.ForUpdated(session);
    }

    private async Task<SessionStoreResult> RestoreAsync(
        Session session,
        Guid trainerId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        // TransitionOnceAsync já devolveu AlreadyInRequestedState quando
        // session.Status == Scheduled; chegar aqui garante que não está.

        // Sessões passadas são correções históricas e não voltam a bloquear a agenda.
        if (session.StartsAt > AsOffset(now))
        {
            var scheduleFailure = await ValidateScheduleAsync(
                trainerId,
                session.ClientId,
                session.StartsAt,
                session.DurationMinutes,
                session.Id,
                cancellationToken
            );
            if (scheduleFailure is not null)
                return scheduleFailure;
        }

        if ((session.Status == SessionStatus.Completed ||
            session.Status == SessionStatus.NoShow) &&
            session.ClientSessionPackId.HasValue)
        {
            var pack = await LockPackAsync(
                trainerId,
                session.ClientId,
                session.ClientSessionPackId.Value,
                cancellationToken
            );
            if (pack is null)
                return SessionStoreResult.For(SessionStoreResult.Status.PackNotAvailable);
            if (pack.SessionsRemaining >= pack.SessionsTotal)
                return SessionStoreResult.For(SessionStoreResult.Status.InvalidState);

            pack.RestoreSession(now);
        }

        session.Restore(now);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (IsScheduleConflict(exception, PersistenceOperation.RestoreSession))
        {
            return SessionStoreResult.For(SessionStoreResult.Status.TrainerScheduleConflict);
        }

        return SessionStoreResult.ForUpdated(session);
    }

    private bool IsScheduleConflict(
        DbUpdateException exception,
        PersistenceOperation operation)
    {
        var translated = _constraintTranslator.TryTranslate(
            exception,
            operation,
            out var error
        );
        return translated && error?.Code == "session_schedule_conflict";
    }

    private async Task<SessionStoreResult?> ValidateScheduleAsync(
        Guid trainerId,
        Guid clientId,
        DateTimeOffset startsAt,
        int durationMinutes,
        Guid? excludeSessionId,
        CancellationToken cancellationToken)
    {
        var timezone = await _timeZoneProvider.GetRequiredAsync(trainerId, cancellationToken);
        var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(startsAt, timezone).DateTime);
        var (dayStart, dayEnd) = ToUtcDayRange(localDate, timezone);

        var scheduled = _dbContext.Sessions
            .Where(session => session.OwnerTrainerId == trainerId)
            .Where(session => session.Status == SessionStatus.Scheduled);
        if (excludeSessionId.HasValue)
            scheduled = scheduled.Where(session => session.Id != excludeSessionId.Value);

        if (await scheduled.AnyAsync(
            session => session.ClientId == clientId &&
                session.StartsAt >= dayStart && session.StartsAt < dayEnd,
            cancellationToken))
            return SessionStoreResult.For(SessionStoreResult.Status.ClientDayConflict);

        var endsAt = startsAt.AddMinutes(durationMinutes);
        if (await scheduled.AnyAsync(
            session => session.StartsAt < endsAt &&
                session.StartsAt.AddMinutes(session.DurationMinutes) > startsAt,
            cancellationToken))
            return SessionStoreResult.For(SessionStoreResult.Status.TrainerScheduleConflict);
        return null;
    }

    private async Task LockTrainerAsync(Guid trainerId, CancellationToken cancellationToken)
    {
        var lockedId = await _dbContext.Database.SqlQuery<Guid>(
            $"SELECT id AS \"Value\" FROM users WHERE id = {trainerId} AND role = 'trainer' AND is_deleted = false FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (lockedId == Guid.Empty)
            throw new InvalidOperationException(
                "The effective personal trainer must exist before writing sessions."
            );
    }

    private Task<Client?> LockClientAsync(
        Guid trainerId,
        Guid clientId,
        CancellationToken cancellationToken) =>
        _dbContext.Clients
            .FromSqlInterpolated($$"""
                SELECT *
                FROM clients
                WHERE owner_trainer_id = {{trainerId}}
                    AND id = {{clientId}}
                    AND is_deleted = false
                FOR UPDATE
                """)
            .SingleOrDefaultAsync(cancellationToken);

    private Task<Session?> LockSessionAsync(
        Guid trainerId,
        Guid sessionId,
        CancellationToken cancellationToken) =>
        _dbContext.Sessions
            .FromSqlInterpolated($$"""
                SELECT *
                FROM sessions
                WHERE owner_trainer_id = {{trainerId}}
                    AND id = {{sessionId}}
                    AND is_deleted = false
                FOR UPDATE
                """)
            .SingleOrDefaultAsync(cancellationToken);

    private Task<ClientSessionPack?> LockUsablePackAsync(
        Guid trainerId,
        Guid clientId,
        Guid packId,
        CancellationToken cancellationToken) =>
        _dbContext.ClientSessionPacks
            .FromSqlInterpolated($$"""
                SELECT *
                FROM client_session_packs
                WHERE owner_trainer_id = {{trainerId}}
                    AND client_id = {{clientId}}
                    AND id = {{packId}}
                    AND is_deleted = false
                    AND sessions_remaining > 0
                FOR UPDATE
                """)
            .SingleOrDefaultAsync(cancellationToken);

    private Task<ClientSessionPack?> LockPackAsync(
        Guid trainerId,
        Guid clientId,
        Guid packId,
        CancellationToken cancellationToken) =>
        _dbContext.ClientSessionPacks
            .FromSqlInterpolated($$"""
                SELECT *
                FROM client_session_packs
                WHERE owner_trainer_id = {{trainerId}}
                    AND client_id = {{clientId}}
                    AND id = {{packId}}
                    AND is_deleted = false
                FOR UPDATE
                """)
            .SingleOrDefaultAsync(cancellationToken);

    private static (DateTimeOffset Start, DateTimeOffset End) ToUtcDayRange(
        DateOnly localDate,
        TimeZoneInfo timezone)
    {
        var localStart = localDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var localEnd = localDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return (
            new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localStart, timezone), TimeSpan.Zero),
            new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localEnd, timezone), TimeSpan.Zero)
        );
    }

    private static SessionStatus TargetStatus(SessionTransition transition) =>
        transition switch
        {
            SessionTransition.Complete => SessionStatus.Completed,
            SessionTransition.CancelByTrainer => SessionStatus.CancelledByTrainer,
            SessionTransition.CancelByClient => SessionStatus.CancelledByClient,
            SessionTransition.MarkNoShow => SessionStatus.NoShow,
            SessionTransition.Restore => SessionStatus.Scheduled,
            _ => throw new ArgumentOutOfRangeException(nameof(transition))
        };

    private static void ApplyTransition(Session session, SessionTransition transition, DateTime now)
    {
        switch (transition)
        {
            case SessionTransition.Complete:
                session.Complete(now);
                break;
            case SessionTransition.CancelByTrainer:
                session.CancelByTrainer(now);
                break;
            case SessionTransition.CancelByClient:
                session.CancelByClient(now);
                break;
            case SessionTransition.MarkNoShow:
                session.MarkNoShow(now);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(transition));
        }
    }

    private async Task<SessionStoreResult> ExecuteTransactionAsync(
        Func<Task<SessionStoreResult>> operation,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            _dbContext.ChangeTracker.Clear();
            await using var transaction = await _dbContext.Database
                .BeginTransactionAsync(cancellationToken);

            try
            {
                var result = await operation();
                if (result.Kind is SessionStoreResult.Status.Created or
                    SessionStoreResult.Status.Updated)
                    await transaction.CommitAsync(cancellationToken);
                else
                    await transaction.RollbackAsync(CancellationToken.None);
                return result;
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw;
            }
        });
    }

    private static DateTimeOffset AsOffset(DateTime utc) =>
        new(DateTime.SpecifyKind(utc, DateTimeKind.Utc));

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
