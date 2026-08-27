using Microsoft.Extensions.Options;

namespace Api.Configuration;

/// <summary>Regista a pólitica CORS estrita usada pelo frontend autorizado.</summary>
public static class ApiCorsPolicy
{
    public const string PolicyName = "Frontend";

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
                        "X-Correlation-ID"
                    )
                    .WithExposedHeaders(
                        "X-Correlation-ID",
                        "Retry-After",
                        "Location"
                    );

                // O cookie de refresh é enviado entre origens explicitamente autorizados.
                policy.AllowCredentials();
            });
        });

        return services;
    }
}
