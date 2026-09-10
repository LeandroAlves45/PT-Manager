using Application.Features.Billing.Abstractions;

namespace Infrastructure.Payments.Stripe;

/// <summary>Impede exceções e mensagens Stripe de atravessarem Infrastructure.</summary>
internal static class StripeFailureMapper
{
    public static BillingGatewayStatus Map(global::Stripe.StripeException exception)
    {
        var status = (int?)exception.HttpStatusCode;
        if (status is 408 or 409 or 429 || status >= 500)
            return BillingGatewayStatus.TransientFailure;

        if (status == 404)
            return BillingGatewayStatus.NotFound;

        return BillingGatewayStatus.InvalidResponse;
    }
}
