using Domain.Exceptions;

namespace Domain.ValueObjects;

/// <summary>
/// Estado técnico de um vídeo de exercício gerido. Persistido em minúsculas
/// conforme a check constraint definida no esquema.
/// </summary>
public sealed record ExerciseVideoStatus
{
    public string Value { get; }

    private ExerciseVideoStatus(string value) => Value = value;

    /// <summary>Autorização de upload emitida; o objeto pode ainda não existir.</summary>
    public static ExerciseVideoStatus Pending = new("pending");
    public static ExerciseVideoStatus Processing = new("processing");
    public static ExerciseVideoStatus Ready = new("ready");
    public static ExerciseVideoStatus Rejected = new("rejected");
    public static ExerciseVideoStatus Failed = new("failed");

    /// <summary>Indica que ainda existe trabalho técnico por concluir.</summary>
    public bool IsInFlight => this == Pending || this == Processing;

    /// <summary>Indica um estado final sem regresso.</summary>
    public bool IsTerminal => this == Ready || this == Rejected || this == Failed;

    public bool CanTransitionTo(ExerciseVideoStatus next)
    {
        ArgumentNullException.ThrowIfNull(next);

        return (this == Pending && (next == Processing || next == Rejected || next == Failed)) ||
            (this == Processing && (next == Ready || next == Rejected || next == Failed));
    }

    public static ExerciseVideoStatus FromString(string value) =>
        value switch
        {
            "pending" => Pending,
            "processing" => Processing,
            "ready" => Ready,
            "rejected" => Rejected,
            "failed" => Failed,
            _ => throw new DomainException($"Invalid exercise video status: {value}")
        };
}
