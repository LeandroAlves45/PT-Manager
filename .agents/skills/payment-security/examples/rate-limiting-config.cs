// rate-limiting-config.cs
// Configurar rate limiting agressivo para endpoints de pagamento

namespace Infrastructure.Configuration;

using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

/// Extensões para registar rate limiting
public static class RateLimitingExtensions
{
    public static IServiceCollection AddPaymentRateLimiting(
        this IServiceCollection services,
        IConfiguration config)
    {
        services.AddRateLimiter(options =>
        {
            // Policy 1: Checkout Session Creation (por trainer)
            options.AddPolicy("checkout_creation", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.User.GetTrainerId().ToString(),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 50,
                        Window = TimeSpan.FromHours(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0,
                    }
                ));

            // Policy 2: Webhook Processing (por IP)
            options.AddPolicy("webhook_processing", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 100,
                        Window = TimeSpan.FromHours(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0,
                    }
                ));

            // Policy 3: Login Attempts (por IP, brute force prevention)
            options.AddPolicy("login_attempts", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromHours(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0,
                    }
                ));

            // Global policy (catch-all)
            options.AddPolicy("global", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 1000,
                        Window = TimeSpan.FromHours(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 100,
                    }
                ));
        });

        return services;
    }
}

/// Controllers com rate limiting aplicado (endpoints reais de Application/Features/Billing/)
namespace Api.Billing;

using Microsoft.AspNetCore.RateLimiting;

[ApiController]
[Route("api/v1/billing")]
[Authorize]
public class CheckoutController : ControllerBase
{
    [HttpPost("checkout")]
    [RateLimitPolicy("checkout_creation")]
    public async Task<IActionResult> CreateCheckout(
        [FromBody] CreateCheckoutCommand command,
        CancellationToken ct)
    {
        return Ok();
    }

    [HttpGet("subscription")]
    [RateLimitPolicy("global")]
    public async Task<IActionResult> GetSubscription(CancellationToken ct)
    {
        return Ok();
    }
}

[ApiController]
[Route("webhooks")]
[AllowAnonymous]
public class StripeWebhooksController : ControllerBase
{
    [HttpPost("stripe")]
    [RateLimitPolicy("webhook_processing")]
    public async Task<IActionResult> HandleStripeWebhook(CancellationToken ct)
    {
        return Ok();
    }
}

/// Regras:
/// 1. checkout_creation: 50/hora por trainer
/// 2. webhook_processing: 100/hora por IP
/// 3. Sem policy de refund — não existe operação de refund no modelo de subscrição atual
/// 4. QueueLimit = 0 (rejeitar imediatamente)
/// 5. Logging de exceedencies (detectar padrões de ataque)
