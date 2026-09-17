using Domain.Entities.Nutrition;
using FluentValidation;

namespace Application.Validation;

/// <summary>
/// Regras de nutrição partilhadas pelos validators de alimentos privados e globais, para que
/// o limite da porção padrão tenha uma única fonte (o Domain).
/// </summary>
internal static class NutritionValueValidationRules
{
    internal static IRuleBuilderOptions<T, decimal?> MustBeValidDefaultServing<T>(
        this IRuleBuilder<T, decimal?> rule,
        string errorCode) =>
        rule.Must(value => !value.HasValue ||
            value.Value > 0m &&
            value.Value <= Food.MaxDefaultServingGrams &&
            decimal.Round(value.Value, 2) == value.Value)
        .WithErrorCode(errorCode)
        .WithMessage(
            "Default serving must be greater than 0 and at most 1000 grams, with up to two decimals.");
}
