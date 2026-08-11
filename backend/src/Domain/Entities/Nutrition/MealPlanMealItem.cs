using Domain.Exceptions;
namespace Domain.Entities.Nutrition;

/// <summary>
/// Alimento prescrito numa refeição, em gramas e com posição.
/// </summary>
public class MealPlanMealItem
{
    public Guid Id { get; private set; }
    public Guid MealPlanMealId { get; private set; }
    public Guid FoodId { get; private set; }
    public decimal QuantityInGrams { get; private set; }
    public int OrderNumber { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private MealPlanMealItem() { } // EF Core

    /// <summary>Criado apenas via MealPlanMeal.AddItem() -> construtor internal.</summary>
    internal MealPlanMealItem(
        Guid mealPlanMealId,
        Guid foodId,
        decimal quantityInGrams,
        int orderNumber,
        DateTime now
    )
    {
        Validate(mealPlanMealId, foodId, quantityInGrams, orderNumber);

        Id = Guid.NewGuid();
        MealPlanMealId = mealPlanMealId;
        FoodId = foodId;
        QuantityInGrams = quantityInGrams;
        OrderNumber = orderNumber;
        CreatedAt = now;
        UpdatedAt = now;
    }

    /// <summary>Atualiza alimento.</summary>
    internal void Update(
        Guid foodId,
        decimal quantityInGrams,
        int orderNumber,
        DateTime now
    )
    {
        ValidateReference(foodId, quantityInGrams, orderNumber);
        FoodId = foodId;
        QuantityInGrams = quantityInGrams;
        OrderNumber = orderNumber;
        UpdatedAt = now;
    }

    /// <summary>Muda apenas a ordem do alimento na refeição.</summary>
    internal void ChangeOrder(int orderNumber, DateTime now)
    {
        if (orderNumber <= 0)
            throw new DomainException("Meal item order number must be greater than zero.");

        OrderNumber = orderNumber;
        UpdatedAt = now;
    }

    private static void Validate(
        Guid mealPlanMealId,
        Guid foodId,
        decimal quantityInGrams,
        int orderNumber
    )
    {
        if (mealPlanMealId == Guid.Empty)
            throw new DomainException("Meal ID is required.");
        ValidateReference(foodId, quantityInGrams, orderNumber);
    }

    private static void ValidateReference(
        Guid foodId,
        decimal quantityInGrams,
        int orderNumber
    )
    {
        if (foodId == Guid.Empty)
            throw new DomainException("Food ID is required.");
        if (quantityInGrams <= 0m)
            throw new DomainException("Meal item quantity must be greater than zero.");
        if (orderNumber <= 0)
            throw new DomainException("Meal item order number must be greater than zero.");
    }
}
