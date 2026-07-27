using Domain.Exceptions;
using Domain.ValueObjects;
namespace Domain.Entities.Nutrition;

/// <summary>
/// Plano alimentar de um cliente num intervalo de datas, com alvos diários de macros.
/// </summary>
public class MealPlan
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
    /// <summary>Alvo diário de macros (protein, carbs, fats) em gramas.</summary>
    public MacroSummary Targets { get; private set; } = null!;

    public bool IsActive { get; private set; }
    public bool IsArchived { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    /// <summary>Refeições do plano, ordenadas por OrderNumber.</summary>
    public IReadOnlyCollection<MealPlanMeal> Meals => _meals;
    private readonly List<MealPlanMeal> _meals = new();

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
        MacroSummary targets,
        DateTime now
    )
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Meal Plan name is required");

        var normalizedName = name.Trim();
        if (normalizedName.Length > 255)
            throw new DomainException("Meal Plan name cannot exceed 255 characters");
        if (endsDate.HasValue && endsDate.Value < startsDate)
            throw new DomainException("Meal Plan ends date cannot be before starts date");

        Id = Guid.NewGuid();
        OwnerTrainerId = ownerTrainerId;
        ClientId = clientId;
        Name = normalizedName;
        Description = description;
        StartsDate = startsDate;
        EndsDate = endsDate;
        Targets = targets;
        IsActive = true;
        IsArchived = false;
        IsDeleted = false;
        CreatedAt = now;
        UpdatedAt = now;
    }

    /// <summary>
    /// Adiciona uma refeição ao plano alimentar garantindo ordem única dentro do plano.
    /// (constraint unique_meal_order replicada como invariante de domínio)
    /// </summary>
    public MealPlanMeal AddMeal(string mealType, int orderNumber, DateTime now)
    {
        if (_meals.Any(meals => meals.OrderNumber == orderNumber))
            throw new DomainException("Already exists a meal with the same order number in this meal plan");

        var meal = new MealPlanMeal(this.Id, mealType, orderNumber, now);
        _meals.Add(meal);
        UpdatedAt = now;
        return meal;
    }

    /// <summary>Arquiva o plano alimentar, mantendo-o no histórico do cliente.</summary>
    public void Archive(DateTime now)
    {
        IsArchived = true;
        IsActive = false;
        UpdatedAt = now;
    }

    /// <summary>Reativa o plano alimentar arquivado.</summary>
    public void Reactivate(DateTime now)
    {
        IsArchived = false;
        IsActive = true;
        UpdatedAt = now;
    }

    /// <summary>Remove uma refeição do plano, alimentos e suplementos associados saem com ela.</summary>
    public void RemoveMeal(Guid mealId, DateTime now)
    {
        var meal = _meals.FirstOrDefault(m => m.Id == mealId)
            ?? throw new DomainException("Meal not found in this meal plan");

        _meals.Remove(meal);
        UpdatedAt = now;
    }

    /// <summary>Soft delete -> plano e refeições continuam consultáveis por integridade.</summary>
    public void SoftDelete(DateTime now)
    {
        IsDeleted = true;
        IsActive = false;
        UpdatedAt = now;
    }
}
