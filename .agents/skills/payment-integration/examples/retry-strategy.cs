// retry-strategy.cs
// Estratégia de retry com exponential backoff para Stripe operations

namespace Infrastructure.Stripe;

using Stripe;

/// Cliente HTTP com retry automático
public interface IStripeHttpClientWithRetry
{
    Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation, string operationName);
}

public class StripeHttpClientWithRetry : IStripeHttpClientWithRetry
{
    private readonly ILogger<StripeHttpClientWithRetry> _logger;
    private const int MaxRetries = 3;
    private static readonly int[] BackoffDelaysMs = { 100, 200, 400 };

    public StripeHttpClientWithRetry(ILogger<StripeHttpClientWithRetry> logger)
    {
        _logger = logger;
    }

    public async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> operation,
        string operationName)
    {
        for (int attempt = 0; attempt <= MaxRetries; attempt++)
        {
            try
            {
                _logger.LogInformation("Attempt {Attempt}: {Operation}", attempt + 1, operationName);
                return await operation();
            }
            catch (StripeException ex)
            {
                if (attempt == MaxRetries || !IsTransientError(ex))
                {
                    _logger.LogError(
                        "Operation failed after {Attempts} attempts: {Code} - {Message}",
                        attempt + 1,
                        ex.StripeError?.Code,
                        ex.StripeError?.Message);
                    throw;
                }

                var delayMs = BackoffDelaysMs[attempt];
                _logger.LogWarning(
                    "Transient error on attempt {Attempt}, retrying in {DelayMs}ms: {Code}",
                    attempt + 1,
                    delayMs,
                    ex.StripeError?.Code);

                await Task.Delay(delayMs);
            }
        }

        throw new InvalidOperationException("Retry loop failed unexpectedly");
    }

    /// Determinar se erro é transiente (pode fazer retry)
    private bool IsTransientError(StripeException ex)
    {
        // 429: Rate limit (try later)
        if (ex.StripeResponse?.StatusCode == 429)
            return true;

        // 5xx: Server error (try later)
        if ((int)(ex.StripeResponse?.StatusCode ?? 0) >= 500)
            return true;

        // 4xx: Client error (don't retry)
        // 401: Auth error (don't retry)
        // 400: Invalid request (don't retry)
        return false;
    }
}

/// Uso na implementação do ICheckoutGateway (Infrastructure) — ver create-checkout-session.cs
public class StripeCheckoutGateway : ICheckoutGateway
{
    private readonly IStripeHttpClientWithRetry _httpClient;
    private readonly StripeClient _stripeClient;

    public StripeCheckoutGateway(
        IStripeHttpClientWithRetry httpClient,
        StripeClient stripeClient)
    {
        _httpClient = httpClient;
        _stripeClient = stripeClient;
    }

    public async Task<CheckoutResult> CreateCheckoutAsync(
        CreateCheckoutRequest request,
        CancellationToken ct)
    {
        var checkoutSession = await _httpClient.ExecuteWithRetryAsync(
            async () =>
            {
                var options = new SessionCreateOptions
                {
                    Mode = "subscription",
                    Customer = request.ProviderCustomerId,
                    LineItems = new List<SessionLineItemOptions>
                    {
                        new() { Price = request.Tier.ToStripePriceId(), Quantity = 1 }
                    },
                    SuccessUrl = request.SuccessUrl.ToString(),
                    CancelUrl = request.CancelUrl.ToString(),
                    Metadata = new Dictionary<string, string>
                    {
                        { "trainer_id", request.TrainerId.ToString() },
                        { "operation_id", request.OperationId.ToString() }
                    }
                };

                var requestOptions = new RequestOptions
                {
                    IdempotencyKey = request.IdempotencyKey
                };

                var sessionService = new SessionService(_stripeClient);
                return await sessionService.CreateAsync(options, requestOptions, cancellationToken: ct);
            },
            $"CreateCheckoutSession(trainer={request.TrainerId})");

        return new CheckoutResult(new Uri(checkoutSession.Url), checkoutSession.Customer);
    }
}

/// Regras:
/// 1. Transient (retry): 429, 5xx
/// 2. Non-transient (fail): 4xx, 401, 400
/// 3. Max 3 retries: 100ms, 200ms, 400ms
/// 4. Idempotency key: vem do domínio (checkout:{trainerId}:{operationId}), previne duplicados no retry
/// 5. Logging: every attempt + final result
/// 6. Gateway é a ÚNICA classe que importa o SDK Stripe — o handler de Application nunca o vê
