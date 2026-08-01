using Domain.Exceptions;

namespace Domain.ValueObjects;

/// <summary>
/// Respostas qualitativas de um check-in periódico -> as perguntas que um PT realmente faria ao
/// cliente, para perceber como ele está a evoluir e se está a seguir o plano de treino e nutrição.
/// </summary>
public sealed record CheckInFeedback
{
    private const int MaxTextLength = 2000;

    public string? Appetite { get; private set; }
    public string? Digestion { get; private set; }
    public string? TrainingLoad { get; private set; }
    public string? RecoverySleep { get; private set; }
    public string? EnergyLevels { get; private set; }
    public string? BodyResponse { get; private set; }

    private CheckInFeedback() { }

    public static CheckInFeedback Empty { get; } =
        new(null, null, null, null, null, null);

    public CheckInFeedback(
        string? appetite,
        string? digestion,
        string? trainingLoad,
        string? recoverySleep,
        string? energyLevels,
        string? bodyResponse)
    {
        Appetite = NormalizeAndValidate(appetite, nameof(appetite));
        Digestion = NormalizeAndValidate(digestion, nameof(digestion));
        TrainingLoad = NormalizeAndValidate(trainingLoad, nameof(trainingLoad));
        RecoverySleep = NormalizeAndValidate(recoverySleep, nameof(recoverySleep));
        EnergyLevels = NormalizeAndValidate(energyLevels, nameof(energyLevels));
        BodyResponse = NormalizeAndValidate(bodyResponse, nameof(bodyResponse));
    }

    private static string? NormalizeAndValidate(string? text, string fieldName)
    {
        var normalizedValue = string.IsNullOrWhiteSpace(text) ? null : text.Trim();

        if (normalizedValue is not null && normalizedValue.Length > MaxTextLength)
            throw new DomainException($"{fieldName} cannot exceed {MaxTextLength} characters.");

        return normalizedValue;
    }
}

