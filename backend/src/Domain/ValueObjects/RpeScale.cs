using Domain.Exceptions;

namespace Domain.ValueObjects;

/// <summary>
/// Escala de esforço percebida (RPE) partilhada pela prescrição e pelo registo de séries:
/// valores de 1 a 10 em passos de 0.5.
/// </summary>
public static class RpeScale
{
    public const decimal Min = 1m;
    public const decimal Max = 10m;

    public static bool IsValid(decimal value) =>
        value is >= Min and <= Max && value * 2 == decimal.Truncate(value * 2);

    public static void EnsureValid(decimal? value, string field)
    {
        if (value.HasValue && !IsValid(value.Value))
            throw new DomainException($"{field} must be between 1 and 10 in steps of 0.5.");
    }
}
