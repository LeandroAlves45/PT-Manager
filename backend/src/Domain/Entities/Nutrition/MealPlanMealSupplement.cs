using Domain.Exceptions;
namespace Domain.Entities.Nutrition;

/// <summary>
/// Suplemento associado a uma refeição, com notas livres de dosagem/timing
/// (ex: "dose: 2g", "tomar 30min antes do treino").
/// </summary>
public sealed class MealPlanMealSupplement
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
        if (mealPlanMealId == Guid.Empty)
            throw new DomainException("Meal ID is required.");

        ValidateEditableFields(supplementId, notes, quantity, orderNumber);
        Id = Guid.NewGuid();
        MealPlanMealId = mealPlanMealId;
        SupplementId = supplementId;
        Notes = NormalizeOptional(notes);
        Quantity = quantity;
        OrderNumber = orderNumber;
        CreatedAt = now;
        UpdatedAt = now;
    }

    /// <summary>Atualiza suplemento da refeição.</summary>
    internal void Update(
        Guid supplementId,
        string? notes,
        decimal quantity,
        int orderNumber,
        DateTime now
    )
    {
        ValidateEditableFields(supplementId, notes, quantity, orderNumber);
        SupplementId = supplementId;
        Notes = NormalizeOptional(notes);
        Quantity = quantity;
        OrderNumber = orderNumber;
        UpdatedAt = now;
    }

    internal void ChangeOrder(int orderNumber, DateTime now)
    {
        if (orderNumber <= 0)
            throw new DomainException("Supplement order must be greater than zero.");

        OrderNumber = orderNumber;
        UpdatedAt = now;
    }

    private static void ValidateEditableFields(
        Guid supplementId,
        string? notes,
        decimal quantity,
        int orderNumber
    )
    {
        if (supplementId == Guid.Empty)
            throw new DomainException("Supplement ID is required.");
        if (NormalizeOptional(notes)?.Length > 500)
            throw new DomainException("Supplement notes cannot exceed 500 characters.");
        if (quantity <= 0m)
            throw new DomainException("Supplement quantity must be greater than zero.");
        if (orderNumber <= 0)
            throw new DomainException("Supplement order must be greater than zero.");
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

}
