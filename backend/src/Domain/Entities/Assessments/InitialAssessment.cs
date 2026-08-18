using Domain.Exceptions;
using Domain.ValueObjects;
namespace Domain.Entities.Assessments;

/// <summary>
/// Avaliação inicial de um cliente: dados antropométricos, condição médica,
/// nível de atividade física e objetivos, registados no ínicio do acompanhamento.
/// </summary>
public sealed class InitialAssessment
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
        if (ownerTrainerId == Guid.Empty || clientId == Guid.Empty)
            throw new DomainException("Owner trainer ID and client ID are required.");

        Id = Guid.NewGuid();
        OwnerTrainerId = ownerTrainerId;
        ClientId = clientId;
        ApplyValues(
            weightKg, heightCm, bodyFatPercentage, medicalConditions, fitnessLevel,
            activityLevel, goals, profession, bodyMeasurements, nutritionIntake);
        IsDeleted = false;
        CreatedAt = now;
        UpdatedAt = now;
    }

    /// <summary>Corrige os dados sem alterar identidade, tenant ou criação.</summary>
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
        var normalizedMeasurements = bodyMeasurements ?? BodyMeasurements.Empty;
        var normalizedNutrition = nutritionIntake ?? NutritionIntake.Empty;
        var normalizedMedical = NormalizeOptional(medicalConditions);
        var normalizedFitness = NormalizeRequired(fitnessLevel);
        var normalizedGoals = NormalizeRequired(goals);
        var normalizedProfession = NormalizeOptional(profession);
        ValidateParameters(
            weightKg, heightCm, bodyFatPercentage, normalizedFitness,
            activityLevel, normalizedGoals, normalizedProfession
        );

        if (WeightKg == weightKg &&
            HeightCm == heightCm &&
            BodyFatPercentage == bodyFatPercentage &&
            MedicalConditions == normalizedMedical &&
            FitnessLevel == normalizedFitness &&
            ActivityLevel == activityLevel &&
            Goals == normalizedGoals &&
            Profession == normalizedProfession &&
            BodyMeasurements == normalizedMeasurements &&
            NutritionIntake == normalizedNutrition)
            return;

        SetValues(
            weightKg, heightCm, bodyFatPercentage, normalizedMedical,
            normalizedFitness, activityLevel, normalizedGoals,
            normalizedProfession, normalizedMeasurements, normalizedNutrition
        );
        UpdatedAt = now;
    }

    /// <summary>Marca a avaliação como excluída.</summary>
    public void SoftDelete(DateTime now)
    {
        if (IsDeleted)
            return;
        IsDeleted = true;
        UpdatedAt = now;
    }

    private void ApplyValues(
        decimal weightKg,
        int heightCm,
        decimal? bodyFatPercentage,
        string? medicalConditions,
        string fitnessLevel,
        ActivityLevel activityLevel,
        string goals,
        string? profession,
        BodyMeasurements? bodyMeasurements,
        NutritionIntake? nutritionIntake
    )
    {
        var normalizedFitness = NormalizeRequired(fitnessLevel);
        var normalizedGoals = NormalizeRequired(goals);
        var normalizedProfession = NormalizeOptional(profession);
        ValidateParameters(
            weightKg, heightCm, bodyFatPercentage, normalizedFitness,
            activityLevel, normalizedGoals, normalizedProfession
        );

        SetValues(
            weightKg, heightCm, bodyFatPercentage, NormalizeOptional(medicalConditions),
            normalizedFitness, activityLevel, normalizedGoals,
            normalizedProfession, bodyMeasurements ?? BodyMeasurements.Empty,
            nutritionIntake ?? NutritionIntake.Empty
        );
    }

    private void SetValues(
        decimal weightKg,
        int heightCm,
        decimal? bodyFatPercentage,
        string? medicalConditions,
        string fitnessLevel,
        ActivityLevel activityLevel,
        string goals,
        string? profession,
        BodyMeasurements bodyMeasurements,
        NutritionIntake nutritionIntake
    )
    {
        WeightKg = weightKg;
        HeightCm = heightCm;
        BodyFatPercentage = bodyFatPercentage;
        MedicalConditions = medicalConditions;
        FitnessLevel = fitnessLevel;
        ActivityLevel = activityLevel;
        Goals = goals;
        Profession = profession;
        BodyMeasurements = bodyMeasurements;
        NutritionIntake = nutritionIntake;
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
            throw new DomainException("Weight must be greater than zero.");
        if (heightCm <= 0)
            throw new DomainException("Height must be greater than zero.");
        if (bodyFatPercentage is <= 0 or >= 100)
            throw new DomainException("Body fat percentage must be greater than zero and less than one hundred.");
        if (fitnessLevel.Length is 0 or > 50)
            throw new DomainException("Fitness level must contain between 1 and 50 characters.");
        if (activityLevel is null)
            throw new DomainException("Activity level is required.");
        if (goals.Length == 0)
            throw new DomainException("Goals are required.");
        if (profession is { Length: > 255 })
            throw new DomainException("Profession cannot exceed 255 characters.");
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NormalizeRequired(string? value) => value?.Trim() ?? string.Empty;

    private void EnsureNotDeleted()
    {
        if (IsDeleted)
            throw new DomainException("Cannot update a deleted initial assessment.");
    }
}
