using Domain.Exceptions;

namespace Domain.ValueObjects;

/// <summary>
/// Lista fechada de grupos musculares aceites em escrita. O valor persistido continua a ser
/// texto: códigos separados por vírgula, sem espaços, pela ordem canónica desta lista.
/// </summary>
public static class MuscleGroupCatalog
{
    public static readonly IReadOnlyList<string> Codes =
    [
        "chest",
        "back",
        "lats",
        "traps",
        "lower_back",
        "shoulders",
        "biceps",
        "triceps",
        "forearms",
        "core",
        "glutes",
        "quadriceps",
        "hamstrings",
        "calves"
    ];

    /// <summary>
    /// Normaliza uma lista separada por vírgulas: remove espaços, ignora entradas vazias,
    /// converte para minúsculas, elimina duplicados e ordena pela lista canónica.
    /// </summary>
    public static bool TryNormalize(string? value, out string? normalized)
    {
        normalized = null;
        if (string.IsNullOrWhiteSpace(value))
            return true;

        var requested = value
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(code => code.ToLowerInvariant())
            .ToHashSet(StringComparer.Ordinal);

        if (requested.Any(code => !Codes.Contains(code)))
            return false;

        normalized = requested.Count == 0
            ? null
            : string.Join(",", Codes.Where(requested.Contains));

        return true;
    }

    public static string? Normalize(string? value) =>
        TryNormalize(value, out var normalized)
            ? normalized
            : throw new DomainException("Muscle groups contains an unknown code");
}
