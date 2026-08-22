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
public sealed class Food
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
    public bool IsActive { get; private set; }
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
        if (ownerTrainerId.HasValue && ownerTrainerId.Value == Guid.Empty)
            throw new DomainException("Owner trainer ID cannot be empty.");

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
        IsActive = true;
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

    /// <summary>Controla a disponibilidade sem eliminar referências históricas.</summary>
    public void SetActive(bool isActive, DateTime now)
    {
        if (IsActive == isActive)
            return;
        IsActive = isActive;
        UpdatedAt = now;
    }

    /// <summary>Valida os parâmetros do alimento.</summary>
    private static void ValidateParametersFood(
        string name,
        decimal protein,
        decimal carbs,
        decimal fats,
        decimal? fiber
    )
    {
        if (name.Length is 0 or > 255)
            throw new DomainException("Food name must contain between 1 and 255 characters.");
        if (protein is < 0 or > 100 || carbs is < 0 or > 100 || fats is < 0 or > 100)
            throw new DomainException(
                "Each macronutrient must be between 0 and 100 grams per 100 grams of food."
            );
        if (protein + carbs + fats > 100)
            throw new DomainException(
                "Protein, carbs and fats combined cannot exceed 100 grams per 100 grams of food."
            );
        if (fiber.HasValue && fiber.Value is < 0 or > 100)
            throw new DomainException("Fiber must be between 0 and 100 grams per 100 grams of food.");
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
