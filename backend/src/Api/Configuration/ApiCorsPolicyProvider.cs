using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Options;

namespace Api.Configuration;

/// <summary>
/// Escolhe a política CORS pela origem: a da app nativa recebe
/// <see cref="ApiCorsPolicy.NativeAppPolicyName"/>; todas as outras, a política pedida.
/// </summary>
internal sealed class ApiCorsPolicyProvider : ICorsPolicyProvider
{
    private readonly CorsOptions _options;

    public ApiCorsPolicyProvider(IOptions<CorsOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
    }

    public Task<CorsPolicy?> GetPolicyAsync(HttpContext context, string? policyName)
    {
        ArgumentNullException.ThrowIfNull(context);

        var origins = context.Request.Headers.Origin;
        var name = origins.Count == 1 && ApiCorsPolicy.IsNativeAppOrigin(origins[0])
            ? ApiCorsPolicy.NativeAppPolicyName
            : policyName ?? _options.DefaultPolicyName;

        return Task.FromResult(_options.GetPolicy(name));
    }
}
