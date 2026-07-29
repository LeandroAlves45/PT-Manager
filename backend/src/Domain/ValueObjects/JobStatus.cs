using Domain.Exceptions;

namespace Domain.ValueObjects;

/// <summary>
/// Estado de um DurableJob: Pending, Processing, Completed, Failed, DeadLetter.
/// Persistido em minúsculas ("pending", ...) conforme o CHECK.
/// </summary>
public record JobStatus
{
    public string Value { get; }

    private JobStatus(string value) => Value = value;

    public static readonly JobStatus Pending = new("pending");
    public static readonly JobStatus Processing = new("processing");
    public static readonly JobStatus Completed = new("completed");
    public static readonly JobStatus Failed = new("failed");
    public static readonly JobStatus DeadLetter = new("dead_letter");

    /// <summary>True se a transição para next é legal.</summary>
    public bool CanTransitionTo(JobStatus next) =>
        (this == Pending && next == Processing) ||
        (this == Processing && next == Completed) ||
        (this == Processing && next == Failed) ||
        (this == Processing && next == Pending) ||
        (this == Failed && next == Pending) ||
        (this == Failed && next == DeadLetter);

    /// <summary>Converte a string persistida no VO.</summary>
    public static JobStatus FromString(string value) =>
        value switch
        {
            "pending" => Pending,
            "processing" => Processing,
            "completed" => Completed,
            "failed" => Failed,
            "dead_letter" => DeadLetter,
            _ => throw new DomainException($"Invalid job status: {value}")
        };
}
