using Application.Common.Abstractions;
using Application.Features.Assessments.InitialAssessments.Abstractions;
using Application.Features.Assessments.InitialAssessments.Dtos;
using Application.Results;
using Application.Validation;
using Domain.ValueObjects;
using FluentValidation;

namespace Application.Features.Assessments.InitialAssessments.UpdateInitialAssessment;

/// <summary>Corrige uma avaliação inicial do tenant efetivo.</summary>
public sealed class UpdateInitialAssessmentHandler
{
    private readonly IValidator<UpdateInitialAssessmentCommand> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly IInitialAssessmentStore _store;

    public UpdateInitialAssessmentHandler(
        IValidator<UpdateInitialAssessmentCommand> validator,
        ITenantContext tenantContext,
        IClock clock,
        IInitialAssessmentStore store
    )
    {
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(store);
        _validator = validator;
        _tenantContext = tenantContext;
        _clock = clock;
        _store = store;
    }

    public async Task<Result<InitialAssessmentDto>> HandleAsync(
        UpdateInitialAssessmentCommand command,
        CancellationToken cancellationToken
    )
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result<InitialAssessmentDto>.Failure(validation.ToApplicationError());

        var tenant = AssessmentActorAuthorization.RequireTrainer(_tenantContext);
        if (!tenant.IsSuccess)
            return Result<InitialAssessmentDto>.Failure(tenant.Error!);

        var outcome = await _store.UpdateAsync(
            tenant.Value,
            command.AssessmentId,
            command.WeightKg,
            command.HeightCm,
            command.BodyFatPercentage,
            command.MedicalConditions,
            command.FitnessLevel,
            ActivityLevel.FromString(command.ActivityLevel.Trim()),
            command.Goals,
            command.Profession,
            command.BodyMeasurements.ToDomain(),
            command.NutritionIntake.ToDomain(),
            _clock.UtcNow,
            cancellationToken
        );

        return outcome.ToResult();
    }
}
