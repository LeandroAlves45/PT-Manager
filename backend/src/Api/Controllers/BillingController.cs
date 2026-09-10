using Api.Authorization;
using Api.Configuration;
using Api.Contracts.Billing;
using Api.Http;
using Application.Errors;
using Application.Features.Billing.CreateCheckout;
using Application.Features.Billing.CreateCustomerPortal;
using Application.Features.Billing.GetSubscription;
using Application.Features.Billing.Abstractions;
using Application.Features.Billing.Webhooks;
using Infrastructure.Payments.Stripe;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Options;

namespace Api.Controllers;

/// <summary>Expõe billing apenas para o personal trainer autenticado.</summary>
[Route("api/v1/billing")]
[Authorize(ApiPolicyNames.Trainer)]
[SensitiveResponse]
public sealed class BillingController : ApiControllerBase
{
    [HttpGet("subscription")]
    public Task<IActionResult> GetSubscriptionAsync(
        [FromServices] GetSubscriptionHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(cancellationToken),
            SubscriptionResponse.From);

    [HttpPost("checkout")]
    public Task<IActionResult> CreateCheckoutAsync(
        [FromBody] Api.Contracts.Billing.CreateCheckoutRequest request,
        [FromServices] CreateCheckoutHandler handler,
        CancellationToken cancellationToken)
    {
        var operationId = ReadIdempotencyKey();
        return operationId is null
            ? Task.FromResult(InvalidIdempotencyKey())
            : RespondAsync(
                handler.HandleAsync(
                    new CreateCheckoutCommand(
                        operationId.Value,
                        request.Tier),
                    cancellationToken),
                url => new CreateCheckoutResponse(url.AbsoluteUri));
    }

    [HttpPost("customer-portal")]
    public Task<IActionResult> CreateCustomerPortalAsync(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)]
        Api.Contracts.Billing.CreateCustomerPortalRequest? _,
        [FromServices] CreateCustomerPortalHandler handler,
        CancellationToken cancellationToken)
    {
        var operationId = ReadIdempotencyKey();
        return operationId is null
            ? Task.FromResult(InvalidIdempotencyKey())
            : RespondAsync(
                handler.HandleAsync(
                    new CreateCustomerPortalCommand(
                        operationId.Value),
                    cancellationToken),
                url => new CreateCustomerPortalResponse(url.AbsoluteUri));
    }

    [HttpPost("webhook")]
    [AllowAnonymous]
    [Consumes("application/json")]
    public async Task<IActionResult> ProcessWebhookAsync(
        [FromServices] IPaymentWebhookAuthenticator authenticator,
        [FromServices] ProcessPaymentWebhookHandler handler,
        [FromServices] IOptions<StripeOptions> stripeOptions,
        CancellationToken cancellationToken)
    {
        var signatures = Request.Headers["Stripe-Signature"];
        if (signatures.Count != 1 || string.IsNullOrWhiteSpace(signatures[0]))
            return BadRequest();

        var maximumBodySize = stripeOptions.Value.MaximumWebhookBodySize;
        if (Request.ContentLength > maximumBodySize)
            return StatusCode(StatusCodes.Status413PayloadTooLarge);

        await using var body = new MemoryStream(capacity: Math.Min(maximumBodySize, 16_384));
        var buffer = new byte[8192];

        while (true)
        {
            var read = await Request.Body.ReadAsync(buffer, cancellationToken);
            if (read == 0)
                break;

            if (body.Length + read > maximumBodySize)
                return StatusCode(StatusCodes.Status413PayloadTooLarge);

            await body.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        var outcome = authenticator.Authenticate(body.ToArray(), signatures[0]!);
        return outcome.Status switch
        {
            PaymentWebhookAuthenticationStatus.AuthenticatedIgnored => NoContent(),
            PaymentWebhookAuthenticationStatus.Authenticated =>
                await RespondAsync(
                    handler.HandleAsync(
                        outcome.PaymentEvent!,
                        cancellationToken)),
            PaymentWebhookAuthenticationStatus.Disabled =>
                StatusCode(StatusCodes.Status503ServiceUnavailable),
            _ => BadRequest()
        };
    }

    private Guid? ReadIdempotencyKey()
    {
        if (!Request.Headers.TryGetValue("Idempotency-Key", out var values) || values.Count != 1)
            return null;

        return Guid.TryParseExact(values[0], "D", out var value) && value != Guid.Empty ? value : null;
    }

    private IActionResult InvalidIdempotencyKey() => Problem(
        statusCode: StatusCodes.Status400BadRequest,
        title: "billing_idempotency_key_invalid",
        detail: "Idempotency-Key must contain one UUID in canonical format."
    );
}
