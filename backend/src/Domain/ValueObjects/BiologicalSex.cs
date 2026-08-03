using Domain.Exceptions;

namespace Domain.ValueObjects;

/// <summary>
/// Sexo biológico usado no perfil do cliente e nas fórmulas metabólicas
/// que possuem coeficientes diferentes por sexo.
/// </summary>
public sealed record BiologicalSex
{
    public static readonly BiologicalSex Male = new("male");
    public static readonly BiologicalSex Female = new("female");

    public string Value { get; }

    private BiologicalSex(string value) => Value = value;

    /// <summary>Converte o valor persistido em um VO correspondente.</summary>
    public static BiologicalSex FromString(string value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "male" => Male,
            "female" => Female,
            _ => throw new DomainException("Biological sex must be 'male' or 'female'.")
        };
}
