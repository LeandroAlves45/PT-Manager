namespace Domain.ValueObjects;

/// <summary>
/// Proteína e gordura são decisões do personal trainer. Os hidratos são derivados das
/// kcal restantes para impedir que este modo excessa o alvo energético.
/// </summary>
public sealed record PerKgMacroInput(
    decimal ProteinGramsPerKg,
    decimal FatsGramsPerKg
);
