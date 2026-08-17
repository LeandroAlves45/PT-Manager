using Application.Common.Abstractions;
using Application.Features.Nutrition.Calculations;
using Application.Features.Nutrition.MealPlans.Abstractions;
using Application.Features.Nutrition.MealPlans.Dtos;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.Nutrition.MealPlans.CreateMealPlan;

/// <summary>Cria um plano alimentar com cálculo server-side obrigatório.</summary>
public sealed class CreateMealPlanHandler
{
    private readonly IValidator<CreateMealPlanCommand> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly IMealPlanStore _mealPlanStore;
    private readonly IMealPlanQueries _mealPlanQueries;

    public CreateMealPlanHandler(
        IValidator<CreateMealPlanCommand> validator,
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

    public async Task<Result<MealPlanDetailsDto>> HandleAsync(
        CreateMealPlanCommand command,
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
        var model = new CreateMealPlanWriteModel(
            command.ClientId,
            command.Name,
            command.Description,
            command.StartsDate,
            command.EndsDate,
            NutritionCalculationFactory.CreateSnapshot(command.Calculation, now),
            command.Structure
        );

        var outcome = await _mealPlanStore.CreateAsync(
            tenant.Value,
            model,
            now,
            cancellationToken
        );

        if (outcome.Kind == MealPlanStoreResult.Status.Created)
        {
            var details = await _mealPlanQueries.GetDetailsAsync(
                outcome.MealPlanId!.Value,
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
