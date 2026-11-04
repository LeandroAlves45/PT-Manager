using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Api.Configuration;

/// <summary>
/// Regista as políticas CORS: <see cref="PolicyName"/> para o frontend web (com o cookie de
/// refresh) e <see cref="NativeAppPolicyName"/> para a app Capacitor (Bearer, sem credenciais).
/// </summary>
public static class ApiCorsPolicy
{
    public const string PolicyName = "Frontend";

    /// <summary>Política da app nativa, escolhida pela origem do pedido.</summary>
    public const string NativeAppPolicyName = "NativeApp";

    /// <summary>
    /// Origens fixas da WebView do Capacitor: iOS (<c>capacitor://localhost</c>) e Android
    /// (<c>https://localhost</c>, com <c>androidScheme: https</c>). Qualquer app Capacitor tem a
    /// mesma origem, por isso esta política nunca permite credenciais.
    /// </summary>
    public static readonly IReadOnlyList<string> NativeAppOrigins = ["capacitor://localhost", "https://localhost"];

    /// <summary>True se a origem (já normalizada, sem barra final) é a da app nativa.</summary>
    public static bool IsNativeAppOrigin(string? origin) =>
        origin is not null && NativeAppOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase);

    public static IServiceCollection AddApiCors(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddOptions<ApiCorsOptions>()
            .Bind(configuration.GetRequiredSection(ApiCorsOptions.SectionName))
            .Validate(options => options.HasValidOrigins(),
                "Cors:AllowedOrigins must contain unique HTTPS origins without paths or wildcards.")
            .ValidateOnStart();

        // Lido uma única vez aqui e reutilizado pela policy abaixo, em vez de
        // repetir o parsing da secção dentro do delegate da AddPolicy.
        var cors = configuration.GetRequiredSection(ApiCorsOptions.SectionName).Get<ApiCorsOptions>()
            ?? throw new InvalidOperationException("Cors configuration is missing.");

        services.AddCors(options =>
        {
            options.AddPolicy(PolicyName, policy =>
            {
                if (!cors.HasValidOrigins())
                    throw new OptionsValidationException(
                        ApiCorsOptions.SectionName,
                        typeof(ApiCorsOptions),
                        ["Cors:AllowedOrigins contains an invalid origin."]
                    );

                policy.WithOrigins(cors.AllowedOrigins)
                    .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")
                    .WithHeaders(
                        "Authorization",
                        "Content-Type",
                        "X-CSRF-Token",
                        "X-Correlation-ID",
                        // Obrigatório no checkout e no portal de faturação (BillingController).
                        "Idempotency-Key"
                    )
                    .WithExposedHeaders(
                        "X-Correlation-ID",
                        "Retry-After",
                        "Location"
                    );

                // O cookie de refresh é enviado entre origens explicitamente autorizados.
                policy.AllowCredentials();
            });

            options.AddPolicy(NativeAppPolicyName, policy =>
            {
                // Sem X-CSRF-Token (a app manda-o no corpo) e sem AllowCredentials: a sessão da
                // app vai em Bearer e o refresh token no corpo de /api/v1/native/auth.
                policy.WithOrigins([.. NativeAppOrigins])
                    .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")
                    .WithHeaders("Authorization", "Content-Type", "X-Correlation-ID", "Idempotency-Key")
                    .WithExposedHeaders("X-Correlation-ID", "Retry-After", "Location");
            });
        });

        // UseCors pede sempre a política do frontend; o provider troca-a pela da app quando a
        // origem é a da WebView do Capacitor.
        services.Replace(ServiceDescriptor.Transient<ICorsPolicyProvider, ApiCorsPolicyProvider>());

        return services;
    }
}
