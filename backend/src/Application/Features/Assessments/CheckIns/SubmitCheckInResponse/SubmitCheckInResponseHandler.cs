using Application.Common.Abstractions;
using Application.Features.Assessments.CheckIns.Abstractions;
using Application.Features.Assessments.CheckIns.Dtos;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.Assessments.CheckIns.SubmitCheckInResponse;

/// <summary>Submete a resposta de um check-in do cliente autenticado.</summary>
public sealed class SubmitCheckInResponseHandler
{
    private readonly IValidator<SubmitCheckInResponseCommand> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly ITrainerTimeZoneProvider _timeZoneProvider;
    private readonly ICheckInStore _store;

    public SubmitCheckInResponseHandler(
        IValidator<SubmitCheckInResponseCommand> validator,
        ITenantContext tenantContext,
        IClock clock,
        ITrainerTimeZoneProvider timeZoneProvider,
        ICheckInStore store
    )
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _timeZoneProvider = timeZoneProvider ?? throw new ArgumentNullException(nameof(timeZoneProvider));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<Result<CheckInDto>> HandleAsync(
        SubmitCheckInResponseCommand command,
        CancellationToken cancellationToken
    )
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result<CheckInDto>.Failure(validation.ToApplicationError());

        var actor = AssessmentActorAuthorization.RequireClient(_tenantContext);
        if (!actor.IsSuccess)
            return Result<CheckInDto>.Failure(actor.Error!);

        var now = _clock.UtcNow;
        var timeZone = await _timeZoneProvider.GetRequiredAsync(
            actor.Value.TrainerId, cancellationToken);
        var localToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(
            now, timeZone));

        var outcome = await _store.SubmitResponseAsync(
            actor.Value.TrainerId,
            actor.Value.UserId,
            command.CheckInId,
            command.WeightKg,
            command.BodyFatPercentage,
            command.Notes,
            command.BodyMeasurements.ToDomain(),
            command.Feedback.ToDomain(),
            command.TrainingAdherenceScore,
            command.NutritionAdherenceScore,
            now,
            cancellationToken
        );

        return outcome.ToResult(localToday);
    }
}
