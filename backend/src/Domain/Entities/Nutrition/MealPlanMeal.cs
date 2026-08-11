using Domain.Exceptions;
namespace Domain.Entities.Nutrition;

/// <summary>
/// Refeição de um MealPlan com items e supplements ordenados.
/// </summary>
public class MealPlanMeal
{
    private readonly List<MealPlanMealItem> _items = [];
    private readonly List<MealPlanMealSupplement> _supplements = [];

    public Guid Id { get; private set; }
    public Guid MealPlanId { get; private set; }
    public string MealType { get; private set; } = null!;
    public int OrderNumber { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    /// <summary>Alimentos da refeição.</summary>
    public IReadOnlyCollection<MealPlanMealItem> Items => _items;
    public IReadOnlyCollection<MealPlanMealSupplement> Supplements => _supplements;

    private MealPlanMeal() { } // EF Core

    /// <summary>Criada apenas via MealPlan.AddMeal() -> construtor internal.</summary>
    internal MealPlanMeal(
        Guid mealPlanId,
        string mealType,
        int orderNumber,
        DateTime now
    )
    {
        if (mealPlanId == Guid.Empty)
            throw new DomainException("Meal plan ID is required");

        ValidateDetails(mealType, orderNumber);
        Id = Guid.NewGuid();
        MealPlanId = mealPlanId;
        MealType = mealType.Trim();
        OrderNumber = orderNumber;
        CreatedAt = now;
        UpdatedAt = now;
    }

    /// <summary>Atualiza o tipo e ordem da refeição.</summary>
    internal void Update(string mealType, int orderNumber, DateTime now)
    {
        ValidateDetails(mealType, orderNumber);
        MealType = mealType.Trim();
        OrderNumber = orderNumber;
        UpdatedAt = now;
    }

    internal void ChangeOrder(int orderNumber, DateTime now)
    {
        ValidateOrder(orderNumber);
        OrderNumber = orderNumber;
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
        if (_items.Any(item => item.OrderNumber == orderNumber))
            throw new DomainException("Meal item order number already exists in this meal");

        var item = new MealPlanMealItem(Id, foodId, quantityInGrams, orderNumber, now);
        _items.Add(item);
        UpdatedAt = now;
        return item;
    }

    public void UpdateItem(
        Guid itemId,
        Guid foodId,
        decimal quantityInGrams,
        int orderNumber,
        DateTime now
    )
    {
        var item = RequireItem(itemId);
        if (_items.Any(other => other.Id != itemId && other.OrderNumber == orderNumber))
            throw new DomainException("Meal item order number already exists in this meal");

        item.Update(foodId, quantityInGrams, orderNumber, now);
        UpdatedAt = now;
    }

    public void ReorderItems(IReadOnlyDictionary<Guid, int> finalOrders, DateTime now)
    {
        ValidateFinalOrders(
            _items.Select(item => (item.Id, item.OrderNumber)),
            finalOrders,
            "Item"
        );

        foreach (var (itemId, orderNumber) in finalOrders)
            RequireItem(itemId).ChangeOrder(orderNumber, now);

        UpdatedAt = now;
    }

    public void RemoveItem(Guid itemId, DateTime now)
    {
        _items.Remove(RequireItem(itemId));
        UpdatedAt = now;
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
        if (_supplements.Any(s => s.SupplementId == supplementId))
            throw new DomainException("Supplement already exists in this meal");
        if (_supplements.Any(s => s.OrderNumber == orderNumber))
            throw new DomainException("Supplement order number already exists in this meal");

        var association = new MealPlanMealSupplement(
            Id,
            supplementId,
            notes,
            quantity,
            orderNumber,
            now
        );
        _supplements.Add(association);
        UpdatedAt = now;
        return association;
    }

    /// <summary>Atualiza suplemento da refeição.</summary>
    public void UpdateSupplement(
        Guid associationId,
        Guid supplementId,
        string? notes,
        decimal quantity,
        int orderNumber,
        DateTime now
    )
    {
        var association = RequireSupplement(associationId);
        if (_supplements.Any(other => other.Id != associationId && other.SupplementId == supplementId))
            throw new DomainException("Supplement already exists in this meal");
        if (_supplements.Any(other => other.Id != associationId && other.OrderNumber == orderNumber))
            throw new DomainException("Supplement order number already exists in this meal");

        association.Update(supplementId, notes, quantity, orderNumber, now);
        UpdatedAt = now;
    }

    public void ReorderSupplements(IReadOnlyDictionary<Guid, int> finalOrders, DateTime now)
    {
        ValidateFinalOrders(
            _supplements.Select(item => (item.Id, item.OrderNumber)),
            finalOrders,
            "Supplement"
        );

        foreach (var (associationId, orderNumber) in finalOrders)
            RequireSupplement(associationId).ChangeOrder(orderNumber, now);

        UpdatedAt = now;
    }

    public void RemoveSupplement(Guid associationId, DateTime now)
    {
        _supplements.Remove(RequireSupplement(associationId));
        UpdatedAt = now;
    }

    private MealPlanMealItem RequireItem(Guid itemId) =>
        _items.SingleOrDefault(item => item.Id == itemId)
        ?? throw new DomainException("Item does not belong to this meal.");

    private MealPlanMealSupplement RequireSupplement(Guid associationId) =>
        _supplements.SingleOrDefault(item => item.Id == associationId)
        ?? throw new DomainException("Supplement does not belong to this meal.");

    private static void ValidateDetails(string mealType, int orderNumber)
    {
        if (string.IsNullOrWhiteSpace(mealType) || mealType.Trim().Length > 50)
            throw new DomainException("Meal type must contain between 1 and 50 characters.");
        ValidateOrder(orderNumber);
    }

    private static void ValidateOrder(int orderNumber)
    {
        if (orderNumber <= 0)
            throw new DomainException("Order number must be greater than zero.");
    }

    private static void ValidateFinalOrders(
        IEnumerable<(Guid Id, int Order)> currentOrders,
        IReadOnlyDictionary<Guid, int> requestedOrders,
        string nodeName
    )
    {
        ArgumentNullException.ThrowIfNull(requestedOrders);
        var current = currentOrders.ToDictionary(entry => entry.Id, entry => entry.Order);
        if (requestedOrders.Keys.Any(id => !current.ContainsKey(id)))
            throw new DomainException($"{nodeName} does not belong to this meal.");
        if (requestedOrders.Values.Any(order => order <= 0))
            throw new DomainException($"{nodeName} order must be greater than zero.");

        var final = current
            .Select(entry => requestedOrders.GetValueOrDefault(entry.Key, entry.Value))
            .ToArray();

        if (final.Distinct().Count() != final.Length)
            throw new DomainException($"{nodeName} order numbers must be unique.");
    }
}
