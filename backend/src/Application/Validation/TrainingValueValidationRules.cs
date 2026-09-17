using Domain.ValueObjects;
using FluentValidation;

namespace Application.Validation;

/// <summary>
/// Regras de treino partilhadas por validators de prescrição, registo e catálogo, para que
/// a escala do RPE e a lista de grupos musculares tenham uma única fonte (o Domain).
/// </summary>
internal static class TrainingValueValidationRules
{
    /// <summary>Aceita null ou um valor da escala RPE (1-10, passo 0.5).</summary>
    internal static IRuleBuilderOptions<T, decimal?> MustBeValidRpe<T>(
        this IRuleBuilder<T, decimal?> rule,
        string errorCode) =>
        rule.Must(value => !value.HasValue || RpeScale.IsValid(value.Value))
            .WithErrorCode(errorCode)
            .WithMessage("RPE must be between 1 and 10 in steps of 0.5.");

    /// <summary>Aceita null, vazio ou uma lista separada por vírgulas de catálogos conhecidos.</summary>
    internal static IRuleBuilderOptions<T, string?> MustBeKnownMuscleGroups<T>(
        this IRuleBuilder<T, string?> rule) =>
        rule.Must(value => MuscleGroupCatalog.TryNormalize(value, out _))
            .WithErrorCode("exercise_muscle_groups_invalid")
            .WithMessage("Muscle groups must be a comma-separated list of known codes.");
}
