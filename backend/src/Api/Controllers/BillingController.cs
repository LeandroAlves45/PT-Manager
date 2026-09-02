using Api.Authorization;
using Api.Configuration;
using Api.Contracts.Billing;
using Application.Features.Billing.GetSubscription;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>Expõe apenas a leitura da subscrição do personal trainer autenticado.</summary>
[Route("api/v1/billing")]
[Authorize(ApiPolicyNames.Trainer)]
[SensitiveResponse]
public sealed class BillingController : ApiControllerBase
{
    /// <summary>Devolve o estado da subscrição do tenant efetivo.</summary>
    [HttpGet("subscription")]
    public Task<IActionResult> GetSubscriptionAsync(
        [FromServices] GetSubscriptionHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(cancellationToken),
            SubscriptionResponse.From);
}
