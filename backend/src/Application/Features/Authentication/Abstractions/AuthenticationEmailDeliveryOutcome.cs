namespace Application.Features.Authentication.Abstractions;

/// <summary>Outcome sanitizado da entrega direta de um email de Auth.</summary>
public enum AuthenticationEmailDeliveryOutcome
{
    Sent,
    Unavailable
}
