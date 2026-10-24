using Domain.Exceptions;
using Domain.ValueObjects;
namespace Domain.Entities.Nutrition;

/// <summary>
/// Plano alimentar com decisão nutricional imutável por snapshot.
/// </summary>
public sealed class MealPlan
{
    public Guid Id { get; private set; }
    public Guid OwnerTrainerId { get; private set; }
    public Guid ClientId { get; private set; }
    /// <summary>
    /// Nome do plano alimentar, obrigatório. Ex: "Dias de treino", "Dias de descanso".
    /// </summary>
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public DateOnly StartsDate { get; private set; }
    /// <summary>
    /// Data final do plano alimentar, opcional.
    /// Se não for fornecida, o plano é considerado "ativo" até ser substituído.
    /// </summary>
    public DateOnly? EndsDate { get; private set; }
    public decimal KcalTarget { get; private set; }
    /// <summary>Alvo diário de macros (protein, carbs, fats) em gramas.</summary>
    public MacroSummary Targets { get; private set; } = null!;
    public NutritionCalculationSnapshot CalculationSnapshot { get; private set; } = null!;
    public bool IsActive { get; private set; }
    public bool IsArchived { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    /// <summary>Refeições do plano, ordenadas por OrderNumber.</summary>
    public IReadOnlyCollection<MealPlanMeal> Meals => _meals;
    private readonly List<MealPlanMeal> _meals = new();

    /// <summary>Suplementos do plano fora de uma refeição.</summary>
    public IReadOnlyCollection<MealPlanSupplement> PlanSupplements => _planSupplements;
    private readonly List<MealPlanSupplement> _planSupplements = new();

    private MealPlan() { } // EF Core

    /// <summary>
    /// Cria um plano alimentar para um cliente ativo e não arquivado.
    /// </summary>
    public MealPlan(
        Guid ownerTrainerId,
        Guid clientId,
        string name,
        string? description,
        DateOnly startsDate,
        DateOnly? endsDate,
        NutritionCalculationSnapshot calculationSnapshot,
        DateTime now
    )
    {
        var normalizedName = NormalizeRequiredName(name);
        ValidateIdentifiers(ownerTrainerId, clientId);
        ValidateDates(startsDate, endsDate);
        ArgumentNullException.ThrowIfNull(calculationSnapshot);

        Id = Guid.NewGuid();
        OwnerTrainerId = ownerTrainerId;
        ClientId = clientId;
        Name = normalizedName;
        Description = NormalizeOptional(description);
        StartsDate = startsDate;
        EndsDate = endsDate;
        ApplyCalculation(calculationSnapshot);
        IsActive = true;
        IsArchived = false;
        IsDeleted = false;
        CreatedAt = now;
        UpdatedAt = now;
    }

    /// <summary>Atualiza metadados sem alterar cliente, estado ou cálculo.</summary>
    public void UpdateDetails(
        string name,
        string? description,
        DateOnly startsDate,
        DateOnly? endsDate,
        DateTime now
    )
    {
        EnsureNotDeleted();
        var normalizedName = NormalizeRequiredName(name);
        ValidateDates(startsDate, endsDate);

        Name = normalizedName;
        Description = NormalizeOptional(description);
        StartsDate = startsDate;
        EndsDate = endsDate;
        UpdatedAt = now;
    }

    /// <summary>
    /// Substitui a decisão nutricional inteira. Alterar apenas os targets é proibido
    /// porque destruiria a correspondência com a evidência guardada.
    /// </summary>
    public void ReplaceCalculation(
        NutritionCalculationSnapshot calculationSnapshot,
        DateTime now
    )
    {
        EnsureNotDeleted();
        ArgumentNullException.ThrowIfNull(calculationSnapshot);
        ApplyCalculation(calculationSnapshot);
        UpdatedAt = now;
    }

    /// <summary>
    /// Adiciona uma refeição ao plano alimentar garantindo ordem única dentro do plano.
    /// </summary>
    public MealPlanMeal AddMeal(string mealType, int orderNumber, DateTime now)
    {
        EnsureNotDeleted();
        if (_meals.Any(meals => meals.OrderNumber == orderNumber))
            throw new DomainException("Meal order number already exists in this plan");

        var meal = new MealPlanMeal(Id, mealType, orderNumber, now);
        _meals.Add(meal);
        UpdatedAt = now;
        return meal;
    }

    /// <summary>Atualiza o tipo e a ordem de uma refeição pertencente ao plano.</summary>
    public void UpdateMeal(Guid mealId, string mealType, int orderNumber, DateTime now)
    {
        EnsureNotDeleted();
        var meal = RequireMeal(mealId);
        if (_meals.Any(other => other.Id != mealId && other.OrderNumber == orderNumber))
            throw new DomainException("Meal order number already exists in this plan");

        meal.Update(mealType, orderNumber, now);
        UpdatedAt = now;
    }

    /// <summary>Aplica em lote a ordem final das refeições indicadas.</summary>
    public void ReorderMeals(IReadOnlyDictionary<Guid, int> finalOrders, DateTime now)
    {
        EnsureNotDeleted();
        ArgumentNullException.ThrowIfNull(finalOrders);
        ValidateFinalOrders(
            _meals.Select(meal => (meal.Id, meal.OrderNumber)),
            finalOrders,
            "Meal"
        );

        // A validação do estado final permite swaps sem rejeitar colisões transitórias.
        foreach (var (mealId, orderNumber) in finalOrders)
            RequireMeal(mealId).ChangeOrder(orderNumber, now);

        UpdatedAt = now;
    }

    /// <summary>Remove uma refeição do plano, alimentos e suplementos associados saem com ela.</summary>
    public void RemoveMeal(Guid mealId, DateTime now)
    {
        EnsureNotDeleted();

        _meals.Remove(RequireMeal(mealId));
        UpdatedAt = now;
    }

    /// <summary>
    /// Adiciona um suplemento ao plano, fora das refeições. O mesmo suplemento só aparece uma
    /// vez fora das refeições; dentro de uma refeição é outra prescrição.
    /// </summary>
    public MealPlanSupplement AddPlanSupplement(
        Guid supplementId,
        string? notes,
        decimal quantity,
        int orderNumber,
        DateTime now)
    {
        EnsureNotDeleted();
        if (_planSupplements.Any(item => item.SupplementId == supplementId))
            throw new DomainException("Supplement already exists outside the meals of this plan");
        if (_planSupplements.Any(item => item.OrderNumber == orderNumber))
            throw new DomainException("Supplement order number already exists outside the meals of this plan");

        var supplement = new MealPlanSupplement(Id, supplementId, notes, quantity, orderNumber, now);
        _planSupplements.Add(supplement);
        UpdatedAt = now;
        return supplement;
    }

    /// <summary>Atualiza um suplemento do plano fora das refeições.</summary>
    public void UpdatePlanSupplement(
        Guid planSupplementId,
        Guid supplementId,
        string? notes,
        decimal quantity,
        int orderNumber,
        DateTime now)
    {
        EnsureNotDeleted();
        var supplement = RequirePlanSupplement(planSupplementId);
        if (_planSupplements.Any(other => other.Id != planSupplementId && other.SupplementId == supplementId))
            throw new DomainException("Supplement already exists outside the meals of this plan");
        if (_planSupplements.Any(other => other.Id != planSupplementId && other.OrderNumber == orderNumber))
            throw new DomainException("Supplement order number already exists outside the meals of this plan");

        supplement.Update(supplementId, notes, quantity, orderNumber, now);
        UpdatedAt = now;
    }

    /// <summary>Aplica em lote a ordem final dos suplementos fora das refeições.</summary>
    public void ReorderPlanSupplements(IReadOnlyDictionary<Guid, int> finalOrders, DateTime now)
    {
        EnsureNotDeleted();
        ArgumentNullException.ThrowIfNull(finalOrders);
        ValidateFinalOrders(
            _planSupplements.Select(item => (item.Id, item.OrderNumber)),
            finalOrders,
            "Plan supplement"
        );

        foreach (var (planSupplementId, orderNumber) in finalOrders)
            RequirePlanSupplement(planSupplementId).ChangeOrder(orderNumber, now);

        UpdatedAt = now;
    }

    /// <summary>Retira um suplemento do plano fora das refeições.</summary>
    public void RemovePlanSupplement(Guid planSupplementId, DateTime now)
    {
        EnsureNotDeleted();
        _planSupplements.Remove(RequirePlanSupplement(planSupplementId));
        UpdatedAt = now;
    }

    /// <summary>Obtém uma refeição pertencente ao agregado.</summary>
    public MealPlanMeal GetMeal(Guid mealId)
    {
        EnsureNotDeleted();
        return RequireMeal(mealId);
    }

    /// <summary>Arquiva de forma idempotente e informa se houve transição.</summary>
    public bool Archive(DateTime now)
    {
        EnsureNotDeleted();
        if (IsArchived)
            return false;

        IsActive = false;
        IsArchived = true;
        UpdatedAt = now;
        return true;
    }

    /// <summary>Reativa o plano alimentar arquivado.</summary>
    public bool Reactivate(DateTime now)
    {
        EnsureNotDeleted();
        if (IsActive && !IsArchived)
            return false;

        IsArchived = false;
        IsActive = true;
        UpdatedAt = now;
        return true;
    }

    /// <summary>Marca o plano como eliminado sem apagar fisicamente a árvore persistida.</summary>
    public void SoftDelete(DateTime now)
    {
        if (IsDeleted)
            return;

        IsDeleted = true;
        IsActive = false;
        IsArchived = true;
        UpdatedAt = now;
    }

    private void ApplyCalculation(NutritionCalculationSnapshot snapshot)
    {
        CalculationSnapshot = snapshot;
        KcalTarget = snapshot.TargetKcal;
        Targets = new MacroSummary(
            snapshot.ProteinTargetGrams,
            snapshot.CarbsTargetGrams,
            snapshot.FatsTargetGrams
        );
    }

    private MealPlanSupplement RequirePlanSupplement(Guid planSupplementId) =>
        _planSupplements.SingleOrDefault(item => item.Id == planSupplementId)
        ?? throw new DomainException("Supplement does not belong to this plan");

    private MealPlanMeal RequireMeal(Guid mealId)
    {
        if (mealId == Guid.Empty)
            throw new DomainException("Meal ID is required");

        return _meals.SingleOrDefault(meal => meal.Id == mealId)
            ?? throw new DomainException("Meal does not belong to this plan");
    }

    private static void ValidateFinalOrders(
        IEnumerable<(Guid Id, int Order)> currentOrders,
        IReadOnlyDictionary<Guid, int> requestedOrders,
        string nodeName
    )
    {
        var current = currentOrders.ToDictionary(entry => entry.Id, entry => entry.Order);
        if (requestedOrders.Keys.Any(id => !current.ContainsKey(id)))
            throw new DomainException($"{nodeName} does not belong to this aggregate");
        if (requestedOrders.Values.Any(order => order <= 0))
            throw new DomainException($"{nodeName} order must be greater than zero");

        var final = current
            .Select(entry => requestedOrders.GetValueOrDefault(entry.Key, entry.Value))
            .ToArray();
        if (final.Distinct().Count() != final.Length)
            throw new DomainException($"{nodeName} order numbers must be unique");
    }

    private static void ValidateIdentifiers(
        Guid ownerTrainerId,
        Guid clientId
    )
    {
        if (ownerTrainerId == Guid.Empty)
            throw new DomainException("Owner trainer ID is required");
        if (clientId == Guid.Empty)
            throw new DomainException("Client ID is required");
    }

    private static string NormalizeRequiredName(string? value)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length is 0 or > 255)
            throw new DomainException("Meal plan name must contain between 1 and 255 characters");
        return normalized;
    }

    private static void ValidateDates(DateOnly startsDate, DateOnly? endsDate)
    {
        if (endsDate.HasValue && endsDate.Value < startsDate)
            throw new DomainException("End date cannot be before start date");
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void EnsureNotDeleted()
    {
        if (IsDeleted)
            throw new DomainException("Cannot modify a deleted meal plan.");
    }
}
