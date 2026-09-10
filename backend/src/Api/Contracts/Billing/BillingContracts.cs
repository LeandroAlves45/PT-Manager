using System.Text.Json.Serialization;
using Application.Features.Billing.Dtos;

namespace Api.Contracts.Billing;

/// <summary>Estado da subscrição do personal trainer autenticado.</summary>
public sealed record SubscriptionResponse(
    string Status,
    string Tier,
    int? ClientLimit,
    int CurrentClientCount,
    DateTime? TrialEndsAt)
{
    /// <summary>Projeta a subscrição da Application.</summary>
    public static SubscriptionResponse From(SubscriptionDto value) => new(
        value.Status,
        value.Tier,
        value.ClientLimit,
        value.CurrentClientCount,
        value.TrialEndsAt
    );
}

/// <summary>Payload fechado; redirects nunca são recebidos do caller.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateCheckoutRequest(string Tier);

/// <summary>Url temporária da Checkout Session alojada.</summary>
public sealed record CreateCheckoutResponse(string CheckoutUrl);

/// <summary>Body opcional fechado; qualquer propriedade enviada é rejeitada.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateCustomerPortalRequest;

/// <summary>Url temporária da Customer Portal alojada.</summary>
public sealed record CreateCustomerPortalResponse(string PortalUrl);
