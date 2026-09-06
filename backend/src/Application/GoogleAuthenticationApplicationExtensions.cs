using Application.Features.Authentication.Google.IssueLinkChallenge;
using Application.Features.Authentication.Google.IssueSignInChallenge;
using Application.Features.Authentication.Google.Link;
using Application.Features.Authentication.Google.SignIn;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Application;

/// <summary>Compõe apenas os adapters Google de autenticação externa.</summary>
public static class GoogleAuthenticationApplicationExtensions
{
    public static IServiceCollection AddGoogleAuthenticationApplication(
        this IServiceCollection services)
    {
        services.AddScoped<IssueGoogleLinkChallengeHandler>();
        services.AddScoped<IssueGoogleSignInChallengeHandler>();
        services.AddScoped<GoogleLinkHandler>();
        services.AddScoped<GoogleSignInHandler>();

        // Validators
        services.AddScoped<IValidator<GoogleLinkCommand>, GoogleLinkCommandValidator>();
        services.AddScoped<IValidator<GoogleSignInCommand>, GoogleSignInCommandValidator>();

        return services;
    }
}
