using Domain.Exceptions;

namespace Domain.ValueObjects;

/// <summary>
/// Dados do estilo de vida e nutrição recolhidos na avaliação inicial do cliente.
/// Consiste em: preferências alimentares, restrições alimentares, rotina e hábitos alimentares.
/// Os campos são opcionais.
/// </summary>
public sealed record NutritionIntake
{
    // Limite aplicado a cada campo de texto livre, apenas ter um controlo do tamanho máximo.
    private const int MaxTextLength = 2000;

    public string? FoodPreferences { get; private set; }
    public string? DislikedFoods { get; private set; }
    public string? FoodIntolerances { get; private set; }
    public string? FoodAllergies { get; private set; }
    public string? DietaryRestrictions { get; private set; }
    public string? DailyRoutine { get; private set; }
    public int? SleepQuality { get; private set; }
    public int? Mood { get; private set; }
    public int? StressLevel { get; private set; }
    public decimal? AvgWaterLitersPerDay { get; private set; }
    public string? HungriestTimeOfDay { get; private set; }
    public bool? UsesSupplements { get; private set; }
    public string? CurrentSupplements { get; private set; }
    public string? OtherNotes { get; private set; }

    private NutritionIntake() { }

    public static NutritionIntake Empty { get; } =
        new(null, null, null, null, null, null, null, null, null, null, null, null, null, null);

    public NutritionIntake(
        string? foodPreferences,
        string? dislikedFoods,
        string? foodIntolerances,
        string? foodAllergies,
        string? dietaryRestrictions,
        string? dailyRoutine,
        int? sleepQuality,
        int? mood,
        int? stressLevel,
        decimal? avgWaterLitersPerDay,
        string? hungriestTimeOfDay,
        bool? usesSupplements,
        string? currentSupplements,
        string? otherNotes)
    {
        var normalizedFoodPreferences = NormalizeOptionalText(foodPreferences);
        var normalizedDislikedFoods = NormalizeOptionalText(dislikedFoods);
        var normalizedFoodIntolerances = NormalizeOptionalText(foodIntolerances);
        var normalizedFoodAllergies = NormalizeOptionalText(foodAllergies);
        var normalizedDietaryRestrictions = NormalizeOptionalText(dietaryRestrictions);
        var normalizedDailyRoutine = NormalizeOptionalText(dailyRoutine);
        var normalizedHungriestTimeOfDay = NormalizeOptionalText(hungriestTimeOfDay);
        var normalizedCurrentSupplements = NormalizeOptionalText(currentSupplements);
        var normalizedOtherNotes = NormalizeOptionalText(otherNotes);

        ValidateTextLength(normalizedFoodPreferences, nameof(foodPreferences));
        ValidateTextLength(normalizedDislikedFoods, nameof(dislikedFoods));
        ValidateTextLength(normalizedFoodIntolerances, nameof(foodIntolerances));
        ValidateTextLength(normalizedFoodAllergies, nameof(foodAllergies));
        ValidateTextLength(normalizedDietaryRestrictions, nameof(dietaryRestrictions));
        ValidateTextLength(normalizedDailyRoutine, nameof(dailyRoutine));
        ValidateTextLength(normalizedHungriestTimeOfDay, nameof(hungriestTimeOfDay));
        ValidateTextLength(normalizedCurrentSupplements, nameof(currentSupplements));
        ValidateTextLength(normalizedOtherNotes, nameof(otherNotes));

        if (sleepQuality.HasValue && (sleepQuality < 1 || sleepQuality > 5))
            throw new DomainException("Sleep quality must be between 1 and 5.");

        if (mood.HasValue && (mood < 1 || mood > 5))
            throw new DomainException("Mood must be between 1 and 5.");

        if (stressLevel.HasValue && (stressLevel < 1 || stressLevel > 5))
            throw new DomainException("Stress level must be between 1 and 5.");

        if (avgWaterLitersPerDay.HasValue && avgWaterLitersPerDay <= 0)
            throw new DomainException("Average water intake must be greater than 0.");

        FoodPreferences = normalizedFoodPreferences;
        DislikedFoods = normalizedDislikedFoods;
        FoodIntolerances = normalizedFoodIntolerances;
        FoodAllergies = normalizedFoodAllergies;
        DietaryRestrictions = normalizedDietaryRestrictions;
        DailyRoutine = normalizedDailyRoutine;
        SleepQuality = sleepQuality;
        Mood = mood;
        StressLevel = stressLevel;
        AvgWaterLitersPerDay = avgWaterLitersPerDay;
        HungriestTimeOfDay = normalizedHungriestTimeOfDay;
        UsesSupplements = usesSupplements;
        CurrentSupplements = normalizedCurrentSupplements;
        OtherNotes = normalizedOtherNotes;
    }

    /// <summary>
    /// Evita persistir strings vazias ou espaços como informação aparente e garante
    /// que o limite é avaliado sobre o valor guardado.
    /// </summary>
    private static string? NormalizeOptionalText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void ValidateTextLength(string? value, string fieldName)
    {
        if (value is not null && value.Length > MaxTextLength)
            throw new DomainException($"{fieldName} cannot exceed {MaxTextLength} characters.");
    }
}
