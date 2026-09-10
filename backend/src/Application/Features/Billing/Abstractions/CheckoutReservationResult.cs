using Domain.ValueObjects;

namespace Application.Features.Billing.Abstractions;

/// <summary>Reserva o contexto consistente necessários antes de I/O do Stripe.</summary>
public sealed record CheckoutReservationResult(
    CheckoutReservationStatus Status,
    Guid OperationId = default,
    Guid LeaseOwnerId = default,
    string? TrainerEmail = null,
    string? ProviderCustomerId = null,
    SubscriptionTier? Tier = null,
    DateTime? EffectiveTrialEndsAt = null,
    string? ProviderSessionId = null);
