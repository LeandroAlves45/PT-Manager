namespace Domain.Entities.Nutrition;

/// <summary>
/// Refeição de um plano alimentar. O tipo pertence a um conjunto fechado
/// ("Pequeno-almoço", "Almoço", "Lanche", "Jantar", "Ceia") com CHECK no postgreSQL.
/// </summary>
public class MealPlanMeal
{
    public Guid Id { get; private set; }
    public Guid MealPlanId { get; private set; }
    public string MealType { get; private set; } = null!;
    public int OrderNumber { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    /// <summary>Alimentos da refeição.</summary>
    public IReadOnlyCollection<MealPlanMealItem> Items => _items;
    private readonly List<MealPlanMealItem> _items = new();

    /// <summary>Suplementos da refeição (opcional).</summary>
    public IReadOnlyCollection<MealPlanMealSupplement> Supplements => _supplements;
    private readonly List<MealPlanMealSupplement> _supplements = new();

    private MealPlanMeal() { } // EF Core

    /// <summary>Criada apenas via MealPlan.AddMeal() -> construtor internal.</summary>
    internal MealPlanMeal(
        Guid mealPlanId,
        string mealType,
        int orderNumber,
        DateTime now
    )
    {
        if (string.IsNullOrWhiteSpace(mealType))
            throw new DomainException("Meal type is required");
        if (mealType.Trim().Length > 50)
            throw new DomainException("Meal type cannot exceed 50 characters");
        if (orderNumber < 1)
            throw new DomainException("Meal order number must be greater than 0");

        Id = Guid.NewGuid();
        MealPlanId = mealPlanId;
        MealType = mealType.Trim();
        OrderNumber = orderNumber;
        CreatedAt = now;
        UpdatedAt = now;
    }

    /// <summary>Adiciona um alimento com quantidade em gramas (positivo).</summary>
    public MealPlanMealItem AddItem(
        Guid foodId,
        decimal quantityInGrams,
        int orderNumber,
        DateTime now
    )
    {
        if (quantityInGrams <= 0)
            throw new DomainException("Meal item quantity must be greater than 0");

        var item = new MealPlanMealItem(this.Id, foodId, quantityInGrams, orderNumber, now);
        _items.Add(item);
        UpdatedAt = now;
        return item;
    }

    /// <summary>
    /// Adiciona um suplemento com quantidade em gramas ou cápsulas (positivo e número)
    /// único por refeição (constraint unique_meal_supplement_per_meal replicada como invariante)
    /// </summary>
    public MealPlanMealSupplement AddSupplement(
        Guid supplementId,
        string? notes,
        decimal quantity,
        int orderNumber,
        DateTime now
    )
    {
        if (quantity <= 0)
            throw new DomainException("Meal supplement quantity must be greater than 0");
        if (_supplements.Any(s => s.SupplementId == supplementId))
            throw new DomainException("Already exists a supplement with the same id in this meal");

        var supplement = new MealPlanMealSupplement(
            this.Id,
            supplementId,
            notes,
            quantity,
            orderNumber,
            now
        );
        _supplements.Add(supplement);
        UpdatedAt = now;
        return supplement;
    }
}
