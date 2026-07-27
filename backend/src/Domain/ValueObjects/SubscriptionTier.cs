using Domain.Exceptions;

namespace Domain.ValueObjects;

/// <summary>Tier comercial da subscrição: FREE, STARTER, PRO.</summary>
public record SubscriptionTier
{
    public string Value { get; }

    private SubscriptionTier(string value) => Value = value;

    public static readonly SubscriptionTier Free = new("FREE");
    public static readonly SubscriptionTier Starter = new("STARTER");
    public static readonly SubscriptionTier Pro = new("PRO");

    /// <summary>Converte a string persistida no VO.</summary>
    public static SubscriptionTier FromString(string value) =>
        value switch
        {
            "FREE" => Free,
            "STARTER" => Starter,
            "PRO" => Pro,
            _ => throw new DomainException($"Invalid subscription tier: {value}")
        };
}
