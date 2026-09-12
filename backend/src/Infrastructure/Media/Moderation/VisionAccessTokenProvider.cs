using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Options;

namespace Infrastructure.Media.Moderation;

/// <summary>Fornece o token OAuth usado nos pedidos á Vision API.</summary>
internal interface IVisionAccessTokenProvider
{
    Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken);
}

/// <summary>Obtêm tokens de acesso para a service account configurada.</summary>
internal sealed class VisionAccessTokenProvider : IVisionAccessTokenProvider
{
    private readonly Lazy<ITokenAccess?> _credential;

    public VisionAccessTokenProvider(IOptions<VisionOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var json = options.Value.ServiceAccountJson;
        _credential = new Lazy<ITokenAccess?>(() => CreateCredential(json),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        var credential = _credential.Value;
        if (credential is null)
            return null;

        try
        {
            var token = await credential.GetAccessTokenForRequestAsync(null, cancellationToken);
            return string.IsNullOrWhiteSpace(token) ? null : token;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // A exceção pode conter detalhes do pedido de token; não se propaga
            // nem se regista. O chamador trata a ausência como Unavailable.
            return null;
        }
    }

    private static ITokenAccess? CreateCredential(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return CredentialFactory
                .FromJson<ServiceAccountCredential>(json)
                .ToGoogleCredential()
                .CreateScoped(VisionOptions.Scope);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
