using Domain.Exceptions;

namespace Domain.ValueObjects;

/// <summary>Decisão da plataforma, independente da disponibilidade escolhida pelo personal trainer.</summary>
public sealed record PlatformEnforcementStatus
{
    public static readonly PlatformEnforcementStatus Allowed = new("allowed");
    public static readonly PlatformEnforcementStatus Blocked = new("blocked");

    public string Value { get; }

    private PlatformEnforcementStatus(string value) => Value = value;

    public static PlatformEnforcementStatus FromString(string value) => value switch
    {
        "allowed" => Allowed,
        "blocked" => Blocked,
        _ => throw new DomainException($"Invalid platform enforcement status: {value}.")
    };
}
