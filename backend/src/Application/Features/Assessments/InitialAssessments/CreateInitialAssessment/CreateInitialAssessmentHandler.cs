using Application.Common.Abstractions;
using Application.Features.Assessments.InitialAssessments.Abstractions;
using Application.Features.Assessments.InitialAssessments.Dtos;
using Application.Results;
using Application.Validation;
using Domain.ValueObjects;
using FluentValidation;

namespace Application.Features.Assessments.InitialAssessments.CreateInitialAssessment;

/// <summary>Cria uma avaliação inicial pelo personal trainer autenticado.</summary>
public sealed class CreateInitialAssessmentHandler
{
    private readonly IValidator<CreateInitialAssessmentCommand> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly IInitialAssessmentStore _store;

    public CreateInitialAssessmentHandler(
        IValidator<CreateInitialAssessmentCommand> validator,
        ITenantContext tenantContext,
        IClock clock,
        IInitialAssessmentStore store
    )
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<Result<InitialAssessmentDto>> HandleAsync(
        CreateInitialAssessmentCommand command,
        CancellationToken cancellationToken
    )
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result<InitialAssessmentDto>.Failure(validation.ToApplicationError());

        var tenant = AssessmentActorAuthorization.RequireTrainer(_tenantContext);
        if (!tenant.IsSuccess)
            return Result<InitialAssessmentDto>.Failure(tenant.Error!);

        var outcome = await _store.CreateAsync(
            tenant.Value,
            command.ClientId,
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
