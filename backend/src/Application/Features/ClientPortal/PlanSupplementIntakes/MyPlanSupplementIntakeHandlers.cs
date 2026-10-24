using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Common.Time;
using Application.Results;

namespace Application.Features.ClientPortal.PlanSupplementIntakes;

/// <summary>
/// Devolve os suplementos do plano alimentar escolhido com a toma de hoje. Antes da primeira
/// escolha não há tomas: 404 <c>portal_nutrition_plan_not_selected</c>, mesmo com um só plano.
/// </summary>
public sealed class GetMyPlanSupplementsTodayHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly ITrainerTimeZoneProvider _timeZoneProvider;
    private readonly IMyPlanSupplementIntakeQueries _queries;

    public GetMyPlanSupplementsTodayHandler(
        ITenantContext tenantContext,
        IClock clock,
        ITrainerTimeZoneProvider timeZoneProvider,
        IMyPlanSupplementIntakeQueries queries)
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _timeZoneProvider = timeZoneProvider ?? throw new ArgumentNullException(nameof(timeZoneProvider));
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
    }

    public async Task<Result<MyPlanSupplementsTodayDto>> HandleAsync(CancellationToken cancellationToken)
    {
        var actor = ActorAuthorization.RequireClient(_tenantContext, ClientPortalErrors.ClientOnly);
        if (!actor.IsSuccess)
            return Result<MyPlanSupplementsTodayDto>.Failure(actor.Error!);

        var timeZone = await _timeZoneProvider.GetRequiredAsync(actor.Value.TrainerId, cancellationToken);
        var read = await _queries.GetForDateAsync(
            actor.Value.TrainerId,
            actor.Value.UserId,
            LocalDates.Today(_clock.UtcNow, timeZone),
            cancellationToken);

        return read.Status switch
        {
            MyPlanSupplementsReadStatus.Found => Result<MyPlanSupplementsTodayDto>.Success(read.Today!),
            MyPlanSupplementsReadStatus.ProfileNotAvailable =>
                Result<MyPlanSupplementsTodayDto>.Failure(ClientPortalErrors.ProfileNotAvailable),
            MyPlanSupplementsReadStatus.PlanNotSelected =>
                Result<MyPlanSupplementsTodayDto>.Failure(ClientPortalErrors.NutritionPlanNotSelected),
            _ => Result<MyPlanSupplementsTodayDto>.Failure(ClientPortalErrors.NutritionPlanNotAvailable)
        };
    }
}

/// <summary>Prescrição do plano cuja toma de hoje se marca ou desmarca.</summary>
public sealed record MyPlanSupplementIntakeCommand(Guid ItemId);

/// <summary>Marca a toma de hoje; repetir devolve a mesma toma.</summary>
public sealed class MarkMyPlanSupplementIntakeHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly IMyPlanSupplementIntakeStore _store;

    public MarkMyPlanSupplementIntakeHandler(
        ITenantContext tenantContext,
        IClock clock,
        IMyPlanSupplementIntakeStore store)
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<Result<MyPlanSupplementIntakeDto>> HandleAsync(
        MyPlanSupplementIntakeCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var actor = ActorAuthorization.RequireClient(_tenantContext, ClientPortalErrors.ClientOnly);
        if (!actor.IsSuccess)
            return Result<MyPlanSupplementIntakeDto>.Failure(actor.Error!);
        if (command.ItemId == Guid.Empty)
            return Result<MyPlanSupplementIntakeDto>.Failure(ClientPortalErrors.PlanSupplementNotFound);

        var outcome = await _store.MarkAsync(
            actor.Value.TrainerId,
            actor.Value.UserId,
            command.ItemId,
            _clock.UtcNow,
            cancellationToken);

        return outcome.Status switch
        {
            MyPlanSupplementIntakeStatus.Marked or MyPlanSupplementIntakeStatus.AlreadyMarked =>
                Result<MyPlanSupplementIntakeDto>.Success(outcome.Intake!),
            MyPlanSupplementIntakeStatus.ProfileNotAvailable =>
                Result<MyPlanSupplementIntakeDto>.Failure(ClientPortalErrors.ProfileNotAvailable),
            MyPlanSupplementIntakeStatus.ItemUnavailable =>
                Result<MyPlanSupplementIntakeDto>.Failure(ClientPortalErrors.PlanSupplementUnavailable),
            _ => Result<MyPlanSupplementIntakeDto>.Failure(ClientPortalErrors.PlanSupplementNotFound)
        };
    }
}

/// <summary>Desmarca a toma de hoje; sem toma, responde da mesma forma (idempotente).</summary>
public sealed class UnmarkMyPlanSupplementIntakeHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly IMyPlanSupplementIntakeStore _store;

    public UnmarkMyPlanSupplementIntakeHandler(
        ITenantContext tenantContext,
        IClock clock,
        IMyPlanSupplementIntakeStore store)
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<Result> HandleAsync(
        MyPlanSupplementIntakeCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var actor = ActorAuthorization.RequireClient(_tenantContext, ClientPortalErrors.ClientOnly);
        if (!actor.IsSuccess)
            return Result.Failure(actor.Error!);
        if (command.ItemId == Guid.Empty)
            return Result.Failure(ClientPortalErrors.PlanSupplementNotFound);

        var outcome = await _store.UnmarkAsync(
            actor.Value.TrainerId,
            actor.Value.UserId,
            command.ItemId,
            _clock.UtcNow,
            cancellationToken);

        return outcome.Status switch
        {
            MyPlanSupplementIntakeStatus.Unmarked or MyPlanSupplementIntakeStatus.NotMarked => Result.Success(),
            MyPlanSupplementIntakeStatus.ProfileNotAvailable => Result.Failure(ClientPortalErrors.ProfileNotAvailable),
            _ => Result.Failure(ClientPortalErrors.PlanSupplementNotFound)
        };
    }
}
