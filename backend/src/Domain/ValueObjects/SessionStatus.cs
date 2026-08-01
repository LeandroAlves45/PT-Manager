using Domain.Exceptions;

namespace Domain.ValueObjects;

/// <summary>Estado persistido de uma sessão agendada.</summary>
public sealed record SessionStatus
{
    public string Value { get; }

    private SessionStatus(string value) => Value = value;

    public static readonly SessionStatus Scheduled = new("scheduled");
    public static readonly SessionStatus Completed = new("completed");
    public static readonly SessionStatus CancelledByClient = new("cancelled_by_client");
    public static readonly SessionStatus CancelledByTrainer = new("cancelled_by_trainer");
    public static readonly SessionStatus NoShow = new("no_show");

    /// <summary>Converte o valor persistido para o Value Object conhecido.</summary>
    public static SessionStatus FromString(string value) => value switch
    {
        "scheduled" => Scheduled,
        "completed" => Completed,
        "cancelled_by_client" => CancelledByClient,
        "cancelled_by_trainer" => CancelledByTrainer,
        "no_show" => NoShow,
        _ => throw new DomainException($"Invalid session status: {value}")
    };
}
