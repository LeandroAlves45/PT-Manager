using Application.Common.Abstractions;
using Application.Features.Supplements.Abstractions;
using Application.Features.Supplements.Dtos;
using Application.Results;

namespace Application.Features.Supplements.ListMyTodaySupplementIntakes;

/// <summary>Lista as tomas de hoje (dia local do personal trainer) do cliente autenticado.</summary>
public sealed class ListMyTodaySupplementIntakesHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly ITrainerTimeZoneProvider _timeZoneProvider;
    private readonly ISupplementIntakeQueries _queries;

    public ListMyTodaySupplementIntakesHandler(
        ITenantContext tenantContext,
        IClock clock,
        ITrainerTimeZoneProvider timeZoneProvider,
        ISupplementIntakeQueries queries)
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _timeZoneProvider = timeZoneProvider ?? throw new ArgumentNullException(nameof(timeZoneProvider));
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
    }

    public async Task<Result<MyTodaySupplementIntakesDto>> HandleAsync(
        CancellationToken cancellationToken)
    {
        var actor = SupplementActorAuthorization.RequireClient(_tenantContext);
        if (!actor.IsSuccess)
            return Result<MyTodaySupplementIntakesDto>.Failure(actor.Error!);

        var timeZone = await _timeZoneProvider.GetRequiredAsync(
            actor.Value.TrainerId, cancellationToken);
        var localToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(_clock.UtcNow, timeZone));

        var intakes = await _queries.ListMyForDateAsync(
            actor.Value.TrainerId,
            actor.Value.UserId,
            localToday,
            cancellationToken);

        // Cliente inexistente ou arquivado responde como atribuição inexistente, sem revelar qual.
        return intakes is null
            ? Result<MyTodaySupplementIntakesDto>.Failure(SupplementErrors.AssignmentNotFound)
            : Result<MyTodaySupplementIntakesDto>.Success(intakes);
    }
}
