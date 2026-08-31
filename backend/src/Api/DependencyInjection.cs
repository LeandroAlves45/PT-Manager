using System.Text.Json;
using System.Text.Json.Serialization;
using Api.Authorization;
using Api.Configuration;
using Api.Security;

namespace Api;

/// <summary>Regista apenas as preocupações da fronteira HTTP.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApi(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddControllers(options =>
            {
                options.Filters.Add<RequireOriginFilter>();
            })
            .AddJsonOptions(options => ConfigureJson(options.JsonSerializerOptions));

        services.ConfigureHttpJsonOptions(options => ConfigureJson(options.SerializerOptions));

        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                context.ProblemDetails.Instance ??= context.HttpContext.Request.Path;
                context.ProblemDetails.Extensions["correlation_id"] =
                    context.HttpContext.TraceIdentifier;
            };
        });

        services.AddApiOpenApi();
        services.AddHsts(options =>
        {
            options.Preload = true;
            options.IncludeSubDomains = true;
            options.MaxAge = TimeSpan.FromDays(365);
        });
        services.AddApiForwardedHeaders(configuration, environment);
        services.AddApiCors(configuration);
        services.AddApiJwtBearer(configuration);
        services.AddApiAuthorization();
        services.AddApiRateLimiting();

        services.AddOptions<AuthCookieOptions>()
            .Bind(configuration.GetSection(AuthCookieOptions.SectionName))
            .Validate(
                options => options.IsValid(),
                "Configuration section 'AuthCookies' is invalid")
            .ValidateOnStart();

        services.AddSingleton<AuthCookieWriter>();

        return services;
    }

    private static void ConfigureJson(JsonSerializerOptions options)
    {
        options.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
        options.DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower;
        options.PropertyNameCaseInsensitive = false;
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
    }
}
