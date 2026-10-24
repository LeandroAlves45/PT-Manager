using Domain.Exceptions;
namespace Domain.Entities.Nutrition;

/// <summary>
/// Suplemento do plano alimentar fora de uma refeição, com nota livre (ex.: cafeína, 30
/// minutos antes do treino). Pertence ao plano: quem segue outro plano não o vê.
/// </summary>
public sealed class MealPlanSupplement
{
    public Guid Id { get; private set; }
    public Guid MealPlanId { get; private set; }
    public Guid SupplementId { get; private set; }
    public string? Notes { get; private set; }

    /// <summary>
    /// Quantidade prescrita. A unidade vive no suplemento referenciado
    /// (<c>Supplement.UnitOfMeasure</c>), como nos suplementos de uma refeição.
    /// </summary>
    public decimal Quantity { get; private set; }
    public int OrderNumber { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private MealPlanSupplement() { } // EF Core

    /// <summary>Criado apenas através de <c>MealPlan.AddPlanSupplement</c>.</summary>
    internal MealPlanSupplement(
        Guid mealPlanId,
        Guid supplementId,
        string? notes,
        decimal quantity,
        int orderNumber,
        DateTime now
    )
    {
        if (mealPlanId == Guid.Empty)
            throw new DomainException("Meal plan ID is required.");

        ValidateEditableFields(supplementId, notes, quantity, orderNumber);
        Id = Guid.NewGuid();
        MealPlanId = mealPlanId;
        SupplementId = supplementId;
        Notes = NormalizeOptional(notes);
        Quantity = quantity;
        OrderNumber = orderNumber;
        CreatedAt = now;
        UpdatedAt = now;
    }

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
