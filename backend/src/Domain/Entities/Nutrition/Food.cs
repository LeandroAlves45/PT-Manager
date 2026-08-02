using Domain.Exceptions;

namespace Domain.Entities.Nutrition;

/// <summary>
/// Alimento do catálogo, com macronutrientes por 100g. Pode ser global ou
/// privado de um personal trainer (tenant).
/// </summary>
/// <remarks>
/// A coluna "kcal" é GENERATED ALWAYS AS (protein * 4 + carbs * 4 + fats * 9) STORED
/// no PostgreSQL -> no Domain, é só leitura e o EF Core configura-a como computed column.
/// </remarks>
public class Food
{
    public Guid Id { get; private set; }
    public Guid? OwnerTrainerId { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public decimal Protein { get; private set; }
    public decimal Carbs { get; private set; }
    public decimal Fats { get; private set; }
    /// <summary>Kcal calculadas (protein * 4 + carbs * 4 + fats * 9). Coluna gerada -> só leitura.</summary>
    public decimal Kcal { get; private set; }
    public decimal? Fiber { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private Food() { } // EF Core

    /// <summary>Cria um alimento com macros não negativos e nome obrigatório.</summary>
    public Food(
        Guid? ownerTrainerId,
        string name,
        string? description,
        decimal protein,
        decimal carbs,
        decimal fats,
        decimal? fiber,
        DateTime now
    )
    {
        var normalizedName = name?.Trim() ?? string.Empty;
        ValidateParametersFood(normalizedName, protein, carbs, fats, fiber);

        Id = Guid.NewGuid();
        OwnerTrainerId = ownerTrainerId;
        Name = normalizedName;
        Description = NormalizeOptional(description);
        Protein = protein;
        Carbs = carbs;
        Fats = fats;
        Fiber = fiber;
        IsDeleted = false;
        CreatedAt = now;
        UpdatedAt = now;
    }

    /// <summary>Atualiza dados e macros (mesmas validações do construtor).</summary>
    public void Update(
        string name,
        string? description,
        decimal protein,
        decimal carbs,
        decimal fats,
        decimal? fiber,
        DateTime now
    )
    {
        EnsureNotDeleted();
        var normalizedName = name?.Trim() ?? string.Empty;
        ValidateParametersFood(normalizedName, protein, carbs, fats, fiber);

        Name = normalizedName;
        Description = NormalizeOptional(description);
        Protein = protein;
        Carbs = carbs;
        Fats = fats;
        Fiber = fiber;
        UpdatedAt = now;
    }

    /// <summary>Marca o alimento como apagado (soft delete).</summary>
    public void SoftDelete(DateTime now)
    {
        IsDeleted = true;
        UpdatedAt = now;
    }

    /// <summary>Valida os parâmetros do alimento.</summary>
    private void ValidateParametersFood(
        string name,
        decimal protein,
        decimal carbs,
        decimal fats,
        decimal? fiber
    )
    {
        if (name.Length is 0 or > 255)
            throw new DomainException("Food name must contain between 1 and 255 characters.");
        if (protein < 0 || carbs < 0 || fats < 0 || (fiber.HasValue && fiber.Value < 0))
            throw new DomainException("Nutritional values cannot be negative.");
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void EnsureNotDeleted()
    {
        if (IsDeleted)
            throw new DomainException("Cannot modify a deleted food.");
    }
}
