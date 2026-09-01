using Domain.Exceptions;

namespace Domain.ValueObjects;

/// <summary>Motivo estruturado e estável para bloquear contéudo privado.</summary>
public sealed record PlatformEnforcementReason
{
    public static readonly PlatformEnforcementReason MaliciousContent = new("malicious_content");
    public static readonly PlatformEnforcementReason DangerousInformation =
        new("dangerous_information");
    public static readonly PlatformEnforcementReason DeliberatelyFalseInformation =
        new("deliberately_false_information");
    public static readonly PlatformEnforcementReason ProhibitedContent
        = new("prohibited_content");

    public string Value { get; }

    private PlatformEnforcementReason(string value) => Value = value;

    public static bool IsSupported(string? value) => value is
        "malicious_content" or
        "dangerous_information" or
        "deliberately_false_information" or
        "prohibited_content";

    public static PlatformEnforcementReason FromString(string value) => value switch
    {
        "malicious_content" => MaliciousContent,
        "dangerous_information" => DangerousInformation,
        "deliberately_false_information" => DeliberatelyFalseInformation,
        "prohibited_content" => ProhibitedContent,
        _ => throw new DomainException($"Invalid platform enforcement reason: {value}.")
    };
}
