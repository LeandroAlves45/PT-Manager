namespace Application.Features.Billing.Dtos;

/// <summary>Projeção local da subscription autenticada.</summary>
public sealed record SubscriptionDto(
    string Status,
    string Tier,
    int ClientLimit,
    int CurrentClientCount,
    DateTime? TrialEndsAt
);
