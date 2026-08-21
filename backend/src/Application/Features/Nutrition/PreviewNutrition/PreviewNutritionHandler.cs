using Application.Common.Abstractions;
using Application.Features.Nutrition.Calculations;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.Nutrition.PreviewNutrition;

/// <summary>Produz um preview validado sem persistir estado.</summary>
public sealed class PreviewNutritionHandler
{
    private readonly IValidator<PreviewNutritionCommand> _validator;
    private readonly IClock _clock;

    /// <summary>Inicializa validação e relógio determinístico.</summary>
    public PreviewNutritionHandler(
        IValidator<PreviewNutritionCommand> validator,
        IClock clock
    )
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <summary>Valida, calcula e devolve o resultado sem I/O.</summary>
    public async Task<Result<NutritionCalculationDto>> HandleAsync(
        PreviewNutritionCommand command,
        CancellationToken cancellationToken
    )
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result<NutritionCalculationDto>.Failure(validation.ToApplicationError());

        var snapshot = NutritionCalculationFactory.CreateSnapshot(
            command.Calculation,
            _clock.UtcNow
        );

        var dto = new NutritionCalculationDto(
            snapshot.SchemaVersion,
            snapshot.CalculationOrigin,
            snapshot.CalculatedAt,
            snapshot.EnergyFormula,
            snapshot.WeightKgUsed,
            snapshot.HeightCmUsed,
            snapshot.AgeUsed,
            snapshot.SexUsed,
            snapshot.BodyFatPercentageUsed,
            snapshot.ActivityLevel,
            snapshot.ActivityFactor,
            snapshot.GoalType,
            snapshot.GoalAdjustmentKcal,
            snapshot.RestingEnergyExpenditureKcal,
            snapshot.TotalDailyEnergyExpenditureKcal,
            snapshot.TargetKcal,
            snapshot.MacroDistributionMode,
            snapshot.ProteinTargetGrams,
            snapshot.CarbsTargetGrams,
            snapshot.FatsTargetGrams,
            snapshot.ProteinEnergyPercentage,
            snapshot.CarbsEnergyPercentage,
            snapshot.FatsEnergyPercentage,
            snapshot.CalculatedMacroKcal,
            snapshot.KcalDifference
        );

        // A ausência de stores é uma propriedade funcional do preview.
        return Result<NutritionCalculationDto>.Success(dto);
    }
}
