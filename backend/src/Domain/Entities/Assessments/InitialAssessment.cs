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
    public int Age { get; private set; }
    public string Gender { get; private set; } = null!;
    public decimal WeightKg { get; private set; }
    public int HeightCm { get; private set; }
    public decimal? BodyFatPercentage { get; private set; }
    public string? MedicalConditions { get; private set; }
    public string FitnessLevel { get; private set; } = null!;
    public string Goals { get; private set; } = null!;
    public bool IsDeleted { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private InitialAssessment() { }

    /// <summary>Cria uma avaliação inicial validando apenas os valores presentes.</summary>
    public InitialAssessment(
        Guid ownerTrainerId,
        Guid clientId,
        int age,
        string gender,
        decimal weightKg,
        int heightCm,
        decimal? bodyFatPercentage,
        string? medicalConditions,
        string fitnessLevel,
        string goals,
        DateTime now
    )
    {
        ValidateParameters(age, weightKg, heightCm, bodyFatPercentage, gender, fitnessLevel, goals);

        Id = Guid.NewGuid();
        OwnerTrainerId = ownerTrainerId;
        ClientId = clientId;
        Age = age;
        Gender = gender;
        WeightKg = weightKg;
        HeightCm = heightCm;
        BodyFatPercentage = bodyFatPercentage;
        MedicalConditions = medicalConditions;
        FitnessLevel = fitnessLevel;
        Goals = goals;
        IsDeleted = false;
        CreatedAt = now;
        UpdatedAt = now;
    }

    /// <summary>Atualiza a avaliação.</summary>
    public void Update(
        int age,
        string gender,
        decimal weightKg,
        int heightCm,
        decimal? bodyFatPercentage,
        string? medicalConditions,
        string fitnessLevel,
        string goals,
        DateTime now
    )
    {
        ValidateParameters(age, weightKg, heightCm, bodyFatPercentage, gender, fitnessLevel, goals);

        Age = age;
        Gender = gender;
        WeightKg = weightKg;
        HeightCm = heightCm;
        BodyFatPercentage = bodyFatPercentage;
        MedicalConditions = medicalConditions;
        FitnessLevel = fitnessLevel;
        Goals = goals;
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
        int age,
        decimal weightKg,
        int heightCm,
        decimal? bodyFatPercentage,
        string gender,
        string fitnessLevel,
        string goals
    )
    {
        if (age <= 0)
            throw new DomainException("Age must be positive.");
        if (weightKg <= 0)
            throw new DomainException("Weight invalid.");
        if (heightCm <= 0)
            throw new DomainException("Height must be positive.");
        if (bodyFatPercentage.HasValue && (bodyFatPercentage.Value < 0 || bodyFatPercentage.Value > 100))
            throw new DomainException("Body fat percentage must be between 0 and 100.");
        if (string.IsNullOrWhiteSpace(gender))
            throw new DomainException("Gender cannot be empty.");
        if (string.IsNullOrWhiteSpace(fitnessLevel))
            throw new DomainException("Fitness level cannot be empty.");
        if (string.IsNullOrWhiteSpace(goals))
            throw new DomainException("Goals cannot be empty.");
    }
}
