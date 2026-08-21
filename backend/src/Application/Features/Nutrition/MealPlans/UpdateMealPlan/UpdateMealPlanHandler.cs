using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.Nutrition.Calculations;
using Application.Features.Nutrition.MealPlans.Abstractions;
using Application.Features.Nutrition.MealPlans.Dtos;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.Nutrition.MealPlans.UpdateMealPlan;

/// <summary>Reconcilia a árvore do mesmo plano alimentar de forma atómica.</summary>
public sealed class UpdateMealPlanHandler
{
    private readonly IValidator<UpdateMealPlanCommand> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly IMealPlanStore _mealPlanStore;
    private readonly IMealPlanQueries _mealPlanQueries;

    /// <summary>Inicializa o caso de uso com validação, tenant, relógio e persistência.</summary>
    public UpdateMealPlanHandler(
        IValidator<UpdateMealPlanCommand> validator,
        ITenantContext tenantContext,
        IClock clock,
        IMealPlanStore mealPlanStore,
        IMealPlanQueries mealPlanQueries
    )
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _mealPlanStore = mealPlanStore ?? throw new ArgumentNullException(nameof(mealPlanStore));
        _mealPlanQueries = mealPlanQueries ?? throw new ArgumentNullException(nameof(mealPlanQueries));
    }

    /// <summary>Valida e reconcilia atomicamente a representação final do plano.</summary>
    /// <param name="command">Metadados, cálculo opcional e estrutura final pretendida.</param>
    /// <param name="cancellationToken">Sinal de cancelamento propagado ao I/O.</param>
    /// <returns>Detalhe atualizado ou uma falha esperada.</returns>
    public async Task<Result<MealPlanDetailsDto>> HandleAsync(
        UpdateMealPlanCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result<MealPlanDetailsDto>.Failure(validation.ToApplicationError());

        var actor = ActorAuthorization.RequireTrainer(_tenantContext, NutritionErrors.MealPlanTrainerOnly);
        if (!actor.IsSuccess)
            return Result<MealPlanDetailsDto>.Failure(actor.Error!);

        var now = _clock.UtcNow;
        var replacement = command.Calculation is null
            ? null
            : NutritionCalculationFactory.CreateSnapshot(command.Calculation, now);

        var model = new UpdateMealPlanWriteModel(
            command.MealPlanId,
            command.Name,
            command.Description,
            command.StartsDate,
            command.EndsDate,
            replacement,
            command.Structure
        );

        var outcome = await _mealPlanStore.UpdateAsync(
            actor.Value.TrainerId,
            model,
            now,
            cancellationToken
        );

        if (outcome.Kind == MealPlanStoreResult.Status.Updated)
        {
            var details = await _mealPlanQueries.GetDetailsAsync(
                command.MealPlanId,
                cancellationToken
            );
            return Result<MealPlanDetailsDto>.Success(details
                ?? throw new InvalidOperationException(
                    "A committed MealPlan must be readable by its owner."
                ));
        }

        return outcome.ToDetailsFailure();
    }
}
