using Domain.Exceptions;
using Domain.ValueObjects;
namespace Domain.Entities.Assessments;

/// <summary>
/// Avaliação inicial de um cliente: dados antropométricos, condição médica,
/// nível de atividade física e objetivos, registados no ínicio do acompanhamento.
/// </summary>
public class InitialAssessment
{
    public Guid Id { get; private set; }
    public Guid OwnerTrainerId { get; private set; }
    public Guid ClientId { get; private set; }
    public decimal WeightKg { get; private set; }
    public int HeightCm { get; private set; }
    public decimal? BodyFatPercentage { get; private set; }
    public string? MedicalConditions { get; private set; }
    public string FitnessLevel { get; private set; } = null!;
    public ActivityLevel ActivityLevel { get; private set; } = null!;
    public string Goals { get; private set; } = null!;
    public string? Profession { get; private set; }
    public BodyMeasurements BodyMeasurements { get; private set; } = BodyMeasurements.Empty;
    public NutritionIntake NutritionIntake { get; private set; } = NutritionIntake.Empty;
    public bool IsDeleted { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private InitialAssessment() { }

    /// <summary>Cria uma avaliação inicial validando apenas os valores presentes.</summary>
    public InitialAssessment(
        Guid ownerTrainerId,
        Guid clientId,
        decimal weightKg,
        int heightCm,
        decimal? bodyFatPercentage,
        string? medicalConditions,
        string fitnessLevel,
        ActivityLevel activityLevel,
        string goals,
        string? profession,
        BodyMeasurements? bodyMeasurements,
        NutritionIntake? nutritionIntake,
        DateTime now
    )
    {
        var normalizedProfession = string.IsNullOrWhiteSpace(profession) ? null : profession.Trim();
        ValidateParameters(
            weightKg, heightCm, bodyFatPercentage, fitnessLevel, activityLevel, goals, normalizedProfession
        );

        Id = Guid.NewGuid();
        OwnerTrainerId = ownerTrainerId;
        ClientId = clientId;
        WeightKg = weightKg;
        HeightCm = heightCm;
        BodyFatPercentage = bodyFatPercentage;
        MedicalConditions = NormalizeOptional(medicalConditions);
        FitnessLevel = fitnessLevel.Trim();
        ActivityLevel = activityLevel;
        Goals = goals.Trim();
        Profession = normalizedProfession;
        BodyMeasurements = bodyMeasurements ?? BodyMeasurements.Empty;
        NutritionIntake = nutritionIntake ?? NutritionIntake.Empty;
        IsDeleted = false;
        CreatedAt = now;
        UpdatedAt = now;
    }

    /// <summary>Atualiza a avaliação.</summary>
    public void Update(
        decimal weightKg,
        int heightCm,
        decimal? bodyFatPercentage,
        string? medicalConditions,
        string fitnessLevel,
        ActivityLevel activityLevel,
        string goals,
        string? profession,
        BodyMeasurements? bodyMeasurements,
        NutritionIntake? nutritionIntake,
        DateTime now
    )
    {
        EnsureNotDeleted();
        var normalizedProfession = string.IsNullOrWhiteSpace(profession) ? null : profession.Trim();
        ValidateParameters(
            weightKg, heightCm, bodyFatPercentage, fitnessLevel, activityLevel, goals, normalizedProfession
        );

        WeightKg = weightKg;
        HeightCm = heightCm;
        BodyFatPercentage = bodyFatPercentage;
        MedicalConditions = NormalizeOptional(medicalConditions);
        FitnessLevel = fitnessLevel.Trim();
        ActivityLevel = activityLevel;
        Goals = goals.Trim();
        Profession = normalizedProfession;
        BodyMeasurements = bodyMeasurements ?? BodyMeasurements.Empty;
        NutritionIntake = nutritionIntake ?? NutritionIntake.Empty;
        UpdatedAt = now;
    }

    /// <summary>Marca a avaliação como excluída.</summary>
    public void SoftDelete(DateTime now)
    {
        IsDeleted = true;
        UpdatedAt = now;
    }

    /// <summary>Valida os parâmetros da avaliação inicial.</summary>
    private void ValidateParameters(
        decimal weightKg,
        int heightCm,
        decimal? bodyFatPercentage,
        string fitnessLevel,
        ActivityLevel activityLevel,
        string goals,
        string? profession
    )
    {
        if (weightKg <= 0)
            throw new DomainException("Weight invalid.");
        if (heightCm <= 0)
            throw new DomainException("Height must be positive.");
        if (bodyFatPercentage.HasValue &&
            (bodyFatPercentage.Value <= 0 || bodyFatPercentage.Value >= 100))
            throw new DomainException("Body fat percentage must be between 0 and 100.");
        if (string.IsNullOrWhiteSpace(fitnessLevel) || fitnessLevel.Trim().Length > 50)
            throw new DomainException("Fitness level must contain between 1 and 50 characters.");
        if (activityLevel is null)
            throw new DomainException("Activity level is required.");
        if (fitnessLevel.Length > 50)
            throw new DomainException("Fitness level cannot exceed 50 characters.");
        if (string.IsNullOrWhiteSpace(goals))
            throw new DomainException("Goals cannot be empty.");
        if (profession is not null && profession.Length > 255)
            throw new DomainException("Profession cannot exceed 255 characters.");
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void EnsureNotDeleted()
    {
        if (IsDeleted)
            throw new DomainException("Cannot update a deleted initial assessment.");
    }
}
