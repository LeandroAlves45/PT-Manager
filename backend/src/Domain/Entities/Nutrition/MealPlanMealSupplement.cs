namespace Domain.Entities.Nutrition;

/// <summary>
/// Suplemento associado a uma refeição, com notas livres de dosagem/timing
/// (ex: "dose: 2g", "tomar 30min antes do treino").
/// </summary>
public class MealPlanMealSupplement
{
    public Guid Id { get; private set; }
    public Guid MealPlanMealId { get; private set; }
    public Guid SupplementId { get; private set; }
    public string? Notes { get; private set; }

    /// <summary>
    /// Quantidade associada. A unidade (gramas, cápsulas, ml,
    /// comprimidos) não vive aqui — consultar Supplement.UnitOfMeasure do
    /// suplemento referenciado por SupplementId.
    /// </summary>
    public decimal Quantity { get; private set; }
    public int OrderNumber { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private MealPlanMealSupplement() { } // EF Core

    /// <summary>Criado apenas via MealPlanMeal.AddSupplement() -> construtor internal.</summary>
    internal MealPlanMealSupplement(
        Guid mealPlanMealId,
        Guid supplementId,
        string? notes,
        decimal quantity,
        int orderNumber,
        DateTime now
    )
    {
        // GUARD de quantidade já feito no pai, repete-se aqui por defesa em profundidade
        if (quantity <= 0)
            throw new DomainException("Meal supplement quantity must be greater than 0");

        Id = Guid.NewGuid();
        MealPlanMealId = mealPlanMealId;
        SupplementId = supplementId;
        Notes = notes;
        Quantity = quantity;
        OrderNumber = orderNumber;
        CreatedAt = now;
        UpdatedAt = now;
    }

    /// <summary>Ajusta a quantidade do suplemento na refeição.</summary>
    public void ChangeQuantity(decimal quantity, DateTime now)
    {
        if (quantity <= 0)
            throw new DomainException("Meal supplement quantity must be greater than 0");

        Quantity = quantity;
        UpdatedAt = now;
    }
}
