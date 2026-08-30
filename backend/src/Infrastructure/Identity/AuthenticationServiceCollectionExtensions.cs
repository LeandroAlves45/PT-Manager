using System.Net.Http.Headers;
using System.Text.Json;
using Application.Features.Authentication;
using Application.Features.Authentication.Abstractions;
using Domain.Entities.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Identity;

/// <summary>Compõe apenas os adapters locais de autenticação.</summary>
internal static class AuthenticationServiceCollectionExtensions
{
    internal static IServiceCollection AddAuthenticationInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddIdentityCore<User>(options =>
        {
            options.Password.RequiredLength = 8;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.Lockout.AllowedForNewUsers = true;
            options.User.RequireUniqueEmail = true;
        }).AddUserStore<UserIdentityStore>();

        services.AddSingleton(CreatePolicy(configuration));
        services.AddSingleton<IOpaqueTokenService, OpaqueTokenService>();

        services.AddScoped<IAuthenticationRegistrationStore, AuthenticationRegistrationStore>();
        services.AddScoped<IAuthenticationSessionStore, AuthenticationSessionStore>();
        services.AddScoped<IClientInvitationStore, ClientInvitationStore>();
        services.AddScoped<IEmailConfirmationStore, EmailConfirmationStore>();
        services.AddScoped<IPasswordResetRequestStore, PasswordResetRequestStore>();
        services.AddScoped<IPasswordManagementStore, PasswordManagementStore>();

        AddAccessTokenIssuer(services, configuration);
        AddAuthenticationEmailSender(services, configuration);

        return services;
    }

    private static void AddAccessTokenIssuer(
        IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(
                options => options.IsValid(),
                "Configuration section 'Jwt' is missing or invalid."
            )
            .ValidateOnStart();

        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();
    }

    private static void AddAuthenticationEmailSender(
        IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<ResendOptions>()
            .Bind(configuration.GetSection(ResendOptions.SectionName))
            .Validate(
                options => options.IsValid(),
                "Configuration section 'Resend' is missing or invalid."
            )
            .ValidateOnStart();

        services.AddHttpClient<IAuthenticationEmailSender, ResendAuthenticationEmailSender>(
            (provider, client) =>
            {
                var options = provider
                    .GetRequiredService<Microsoft.Extensions.Options.IOptions<ResendOptions>>()
                    .Value;

                client.BaseAddress = options.BaseAddress;
                client.Timeout = options.Timeout;
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", options.ApiKey);
            })
            .ConfigureHttpClient(client =>
                client.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json")));

        // A API da Resend espera "from", "to", "subject" e "html" em
        // minúsculas. Sem estas opções, PostAsJsonAsync usa o nome PascalCase da
        // propriedade C# e a Resend responde 422.
        services.AddSingleton(new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    private static AuthenticationPolicy CreatePolicy(IConfiguration configuration)
    {
        var section = configuration.GetSection("Authentication");
        return new AuthenticationPolicy(
            ParseInt(section["TrialDays"], 15, "Authentication:TrialDays"),
            ParseDuration(section["EmailConfirmationLifetime"], TimeSpan.FromHours(24),
                "Authentication:EmailConfirmationLifetime"),
            ParseDuration(section["ClientInviteLifetime"], TimeSpan.FromDays(7),
                "Authentication:ClientInviteLifetime"),
            ParseDuration(section["PasswordResetLifetime"], TimeSpan.FromHours(1),
                "Authentication:PasswordResetLifetime"),
            ParseDuration(section["RefreshSessionLifetime"], TimeSpan.FromDays(30),
                "Authentication:RefreshSessionLifetime"));
    }

    private static int ParseInt(string? value, int fallback, string key) =>
        value is null ? fallback : int.TryParse(value, out var parsed) ? parsed
            : throw new InvalidOperationException($"Configuration '{key}' is invalid.");

    private static TimeSpan ParseDuration(string? value, TimeSpan fallback, string key) =>
        value is null ? fallback : TimeSpan.TryParse(value, out var parsed) ? parsed
            : throw new InvalidOperationException($"Configuration '{key}' is invalid.");
}
