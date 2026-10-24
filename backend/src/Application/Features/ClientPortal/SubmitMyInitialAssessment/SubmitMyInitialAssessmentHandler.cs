using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.Assessments;
using Application.Features.Assessments.InitialAssessments.Abstractions;
using Application.Results;
using Application.Validation;
using Domain.ValueObjects;
using FluentValidation;

namespace Application.Features.ClientPortal.SubmitMyInitialAssessment;

/// <summary>
/// Grava a avaliação inicial que o cliente preencheu no portal. A avaliação fica do personal
/// trainer, por rever; o cliente não a volta a ver nem a altera. Uma segunda avaliação, do
/// cliente ou do personal trainer, é recusada. Não há email.
/// </summary>
public sealed class SubmitMyInitialAssessmentHandler
{
    private readonly IValidator<SubmitMyInitialAssessmentCommand> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly IInitialAssessmentStore _store;

    public SubmitMyInitialAssessmentHandler(
        IValidator<SubmitMyInitialAssessmentCommand> validator,
        ITenantContext tenantContext,
        IClock clock,
        IInitialAssessmentStore store)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<Result> HandleAsync(
        SubmitMyInitialAssessmentCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var actor = ActorAuthorization.RequireClient(_tenantContext, ClientPortalErrors.ClientOnly);
        if (!actor.IsSuccess)
            return Result.Failure(actor.Error!);

        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result.Failure(validation.ToApplicationError());

        var outcome = await _store.SubmitByClientAsync(
            actor.Value.TrainerId,
            actor.Value.UserId,
            new InitialAssessmentWriteValues(
                command.WeightKg,
                command.HeightCm,
                command.BodyFatPercentage,
                command.MedicalConditions,
                command.FitnessLevel,
                ActivityLevel.FromString(command.ActivityLevel.Trim()),
                command.Goals,
                command.Profession,
                command.BodyMeasurements.ToDomain(),
                command.NutritionIntake.ToDomain()),
            _clock.UtcNow,
            cancellationToken);

        return outcome.Kind switch
        {
            InitialAssessmentStoreResult.Status.Created => Result.Success(),
            InitialAssessmentStoreResult.Status.ClientNotFound =>
                Result.Failure(ClientPortalErrors.ProfileNotAvailable),
            InitialAssessmentStoreResult.Status.AssessmentAlreadyExists =>
                Result.Failure(AssessmentErrors.InitialAssessmentAlreadyExists),
            _ => throw new InvalidOperationException(
                $"Unexpected initial assessment outcome {outcome.Kind}.")
        };
    }
}
