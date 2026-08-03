namespace Domain.ValueObjects;

/// <summary>Direção do ajuste aplicado ao gasto energético diário.</summary>
public enum NutritionGoalType
{
    Maintenance,
    Deficit,
    Surplus
}

public static class NutritionGoalTypeExtensions
{
    public static string ToKey(this NutritionGoalType goal) => goal switch
    {
        NutritionGoalType.Maintenance => "maintenance",
        NutritionGoalType.Deficit => "deficit",
        NutritionGoalType.Surplus => "surplus",
        _ => throw new ArgumentOutOfRangeException(nameof(goal))
    };
}
