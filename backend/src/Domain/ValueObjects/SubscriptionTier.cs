using Domain.Exceptions;

namespace Domain.ValueObjects;

/// <summary>Tier comercial da subscrição: FREE, STARTER, PRO.</summary>
public record SubscriptionTier
{
    public string Value { get; }
    public int? ClientLimit { get; }

    private SubscriptionTier(string value, int? clientLimit)
    {
        Value = value;
        ClientLimit = clientLimit;
    }

    public static readonly SubscriptionTier Free = new("FREE", 5);
    public static readonly SubscriptionTier Starter = new("STARTER", 25);
    public static readonly SubscriptionTier Pro = new("PRO", null);

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
