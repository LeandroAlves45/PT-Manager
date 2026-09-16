using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.Options;

namespace Infrastructure.Media.Video;

/// <summary>
/// Cria, uma única vez e só quando usado, o cliente S3 configurado para R2.
/// </summary>
internal sealed class R2ClientProvider : IDisposable
{
    private readonly Lazy<IAmazonS3> _client;

    public R2ClientProvider(IOptions<R2Options> options)
        : this(options, httpClientFactory: null)
    {
    }

    /// <summary>Permite aos testes substituir o transporte HTTP sem rede real.</summary>
    internal R2ClientProvider(IOptions<R2Options> options, HttpClientFactory? httpClientFactory)
    {
        ArgumentNullException.ThrowIfNull(options);

        var value = options.Value;
        _client = new Lazy<IAmazonS3>(
            () => Create(value, httpClientFactory),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public IAmazonS3 Client => _client.Value;

    public void Dispose()
    {
        if (_client.IsValueCreated)
            _client.Value.Dispose();
    }

    private static IAmazonS3 Create(R2Options options, HttpClientFactory? httpClientFactory)
    {
        var config = new AmazonS3Config
        {
            ServiceURL = options.ResolveServiceUrl().ToString(),
            AuthenticationRegion = "auto",
            ForcePathStyle = true,
            Timeout = options.Timeout,
            MaxErrorRetry = 2,
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED
        };

        if (httpClientFactory is not null)
            config.HttpClientFactory = httpClientFactory;

        return new AmazonS3Client(
            new BasicAWSCredentials(options.AccessKeyId, options.SecretAccessKey),
            config);
    }
}
