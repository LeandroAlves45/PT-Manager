using Microsoft.Extensions.Options;
using Stripe;

namespace Infrastructure.Payments.Stripe;

/// <summary>
/// Cria uma única instância configurada do SDK apenas quando o Stripe está ativo.
/// </summary>
internal sealed class StripeClientFactory : IDisposable
{
    private readonly StripeOptions _options;
    private readonly Lazy<IStripeClient> _client;
    private HttpClient? _httpClient;

    public StripeClientFactory(IOptions<StripeOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _client = new Lazy<IStripeClient>(Create, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public IStripeClient GetRequiredClient()
    {
        if (!_options.Enabled)
            throw new InvalidOperationException(
                "Stripe client cannot be used while Stripe is disabled.");
        return _client.Value;
    }

    private IStripeClient Create()
    {
        if (string.IsNullOrWhiteSpace(_options.SecretKey))
            throw new InvalidOperationException(
                "Stripe secret key is not configured.");

        _httpClient = new HttpClient { Timeout = _options.Timeout };
        var stripeHttpClient = new SystemNetHttpClient(
            _httpClient,
            _options.MaxNetworkRetries);

        return new StripeClient(
            _options.SecretKey,
            httpClient: stripeHttpClient);
    }

    public void Dispose() => _httpClient?.Dispose();
}
