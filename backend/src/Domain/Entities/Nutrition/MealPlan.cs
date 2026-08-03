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
        var normalizedName = name?.Trim() ?? string.Empty;
        Validate(ownerTrainerId, clientId, normalizedName, startsDate, endsDate, calculationSnapshot);

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

    /// <summary>Remove uma refeição do plano, alimentos e suplementos associados saem com ela.</summary>
    public void RemoveMeal(Guid mealId, DateTime now)
    {
        EnsureNotDeleted();
        var meal = _meals.FirstOrDefault(m => m.Id == mealId)
            ?? throw new DomainException("Meal does not belong to this plan");

        _meals.Remove(meal);
        UpdatedAt = now;
    }

    /// <summary>Arquiva o plano alimentar, mantendo-o no histórico do cliente.</summary>
    public void Archive(DateTime now)
    {
        EnsureNotDeleted();
        IsActive = false;
        IsArchived = true;
        UpdatedAt = now;
    }

    /// <summary>Reativa o plano alimentar arquivado.</summary>
    public void Reactivate(DateTime now)
    {
        EnsureNotDeleted();
        IsArchived = false;
        IsActive = true;
        UpdatedAt = now;
    }

    /// <summary>Soft delete -> plano e refeições continuam consultáveis por integridade.</summary>
    public void SoftDelete(DateTime now)
    {
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

    private static void Validate(
        Guid ownerTrainerId,
        Guid clientId,
        string name,
        DateOnly startsDate,
        DateOnly? endsDate,
        NutritionCalculationSnapshot calculationSnapshot
    )
    {
        if (ownerTrainerId == Guid.Empty)
            throw new DomainException("Owner trainer ID is required");
        if (clientId == Guid.Empty)
            throw new DomainException("Client ID is required");
        if (name.Length is 0 or > 255)
            throw new DomainException("Meal plan name must contain between 1 and 255 characters");
        if (endsDate.HasValue && endsDate.Value < startsDate)
            throw new DomainException("End date cannot be before start date");

        ArgumentNullException.ThrowIfNull(calculationSnapshot);
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void EnsureNotDeleted()
    {
        if (IsDeleted)
            throw new DomainException("Cannot modify a deleted meal plan.");
    }
}
