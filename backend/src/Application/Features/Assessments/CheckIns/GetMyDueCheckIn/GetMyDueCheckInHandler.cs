using Application.Common.Abstractions;
using Application.Features.Assessments.CheckIns.Abstractions;
using Application.Features.Assessments.CheckIns.Dtos;
using Application.Results;

namespace Application.Features.Assessments.CheckIns.GetMyDueCheckIn;

/// <summary>Executa a leitura do check-in devido para o cliente autenticado.</summary>
public sealed class GetMyDueCheckInHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly ITrainerTimeZoneProvider _timeZoneProvider;
    private readonly ICheckInQueries _queries;

    public GetMyDueCheckInHandler(
        ITenantContext tenantContext,
        IClock clock,
        ITrainerTimeZoneProvider timeZoneProvider,
        ICheckInQueries queries
    )
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(timeZoneProvider);
        ArgumentNullException.ThrowIfNull(queries);
        _tenantContext = tenantContext;
        _clock = clock;
        _timeZoneProvider = timeZoneProvider;
        _queries = queries;
    }

    public async Task<Result<CheckInDto?>> HandleAsync(
        GetMyDueCheckInQuery query,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(query);

        var actor = AssessmentActorAuthorization.RequireClient(_tenantContext);
        if (!actor.IsSuccess)
            return Result<CheckInDto?>.Failure(actor.Error!);

        var timeZone = await _timeZoneProvider.GetRequiredAsync(
            actor.Value.TrainerId, cancellationToken);
        var localToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(
            _clock.UtcNow, timeZone));

        var dueCheckIn = await _queries.GetMyDueAsync(
            actor.Value.TrainerId,
            actor.Value.UserId,
            localToday,
            cancellationToken);

        // Null é um resultado válido: hoje pode não existir qualquer formulário a preencher.
        return Result<CheckInDto?>.Success(dueCheckIn);
    }
}
