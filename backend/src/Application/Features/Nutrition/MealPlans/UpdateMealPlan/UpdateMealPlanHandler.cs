using Application.Common.Abstractions;
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
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(mealPlanStore);
        ArgumentNullException.ThrowIfNull(mealPlanQueries);
        _validator = validator;
        _tenantContext = tenantContext;
        _clock = clock;
        _mealPlanStore = mealPlanStore;
        _mealPlanQueries = mealPlanQueries;
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

        var tenant = _tenantContext.GetRequiredTrainerId();
        if (!tenant.IsSuccess)
            return Result<MealPlanDetailsDto>.Failure(tenant.Error!);

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
            tenant.Value,
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

        return outcome.Kind switch
        {
            MealPlanStoreResult.Status.NotFound =>
            Result<MealPlanDetailsDto>.Failure(NutritionErrors.MealPlanNotFound),
            MealPlanStoreResult.Status.StructureReferenceNotFound =>
                Result<MealPlanDetailsDto>.Failure(NutritionErrors.MealPlanStructureReferenceNotFound),
            MealPlanStoreResult.Status.CatalogReferenceNotFound =>
                Result<MealPlanDetailsDto>.Failure(NutritionErrors.CatalogReferenceNotFound),
            MealPlanStoreResult.Status.CatalogReferenceInactive =>
                Result<MealPlanDetailsDto>.Failure(NutritionErrors.CatalogReferenceInactive),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome.Kind))
        };
    }
}
