using Domain.Exceptions;
namespace Domain.Entities.Nutrition;

/// <summary>
/// Alimento numa refeição, com quantidade em gramas e posição.
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
        // GUARD de quantidade já feito no pai, repete-se aqui por defesa em profundidade
        if (quantityInGrams <= 0)
            throw new DomainException("Meal item quantity must be greater than 0");

        Id = Guid.NewGuid();
        MealPlanMealId = mealPlanMealId;
        FoodId = foodId;
        QuantityInGrams = quantityInGrams;
        OrderNumber = orderNumber;
        CreatedAt = now;
        UpdatedAt = now;
    }

    /// <summary>Ajusta a quantidade em gramas do alimento na refeição.</summary>
    public void ChangeQuantity(decimal quantityInGrams, DateTime now)
    {
        if (quantityInGrams <= 0)
            throw new DomainException("Meal item quantity must be greater than 0");

        QuantityInGrams = quantityInGrams;
        UpdatedAt = now;
    }
}
