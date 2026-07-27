using Domain.Exceptions;

namespace Domain.ValueObjects;

/// <summary>
/// Estado de subscrição de um personal trainer: ACTIVE, INACTIVE, SUSPENDED, CANCELLED.
/// Persistido como string (CHECK no PostgreSQL), comparado por valor.
/// </summary>
public record SubscriptionStatus
{
    public string Value { get; }

    private SubscriptionStatus(string value) => Value = value;

    public static readonly SubscriptionStatus Active = new("ACTIVE");
    public static readonly SubscriptionStatus Inactive = new("INACTIVE");
    public static readonly SubscriptionStatus Suspended = new("SUSPENDED");
    public static readonly SubscriptionStatus Cancelled = new("CANCELLED");

    /// <summary>Converte a string persistida no VO.</summary>
    public static SubscriptionStatus FromString(string value) =>
        value switch
        {
            "ACTIVE" => Active,
            "INACTIVE" => Inactive,
            "SUSPENDED" => Suspended,
            "CANCELLED" => Cancelled,
            _ => throw new DomainException($"Invalid subscription status: {value}")
        };
}
