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

        return services;
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
