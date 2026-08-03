using Domain.Exceptions;

namespace Domain.ValueObjects;

/// <summary>
/// Nível de atividade e respetivo multiplicador de manutenção.
/// </summary>
public sealed record ActivityLevel
{
    public static readonly ActivityLevel Sedentary = new("sedentary", 1.200m);
    public static readonly ActivityLevel LightlyActive = new("lightly_active", 1.375m);
    public static readonly ActivityLevel ModeratelyActive = new("moderately_active", 1.550m);
    public static readonly ActivityLevel VeryActive = new("very_active", 1.725m);
    public static readonly ActivityLevel ExtremelyActive = new("extremely_active", 1.900m);

    public string Value { get; }
    public decimal Factor { get; }

    private ActivityLevel(string value, decimal factor)
    {
        Value = value;
        Factor = factor;
    }

    public static ActivityLevel FromString(string value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "sedentary" => Sedentary,
            "lightly_active" => LightlyActive,
            "moderately_active" => ModeratelyActive,
            "very_active" => VeryActive,
            "extremely_active" => ExtremelyActive,
            _ => throw new DomainException("Invalid activity level.")
        };
}
