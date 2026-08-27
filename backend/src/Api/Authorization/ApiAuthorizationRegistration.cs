using Microsoft.AspNetCore.Authorization;

namespace Api.Authorization;

/// <summary>Regista as policies sem configurar o mecanismo JWT.</summary>
public static class ApiAuthorizationRegistration
{
    public static IServiceCollection AddApiAuthorization(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(ApiPolicyNames.Authenticated,
                policy => policy.RequireAuthenticatedUser())

            .AddPolicy(ApiPolicyNames.Trainer,
                policy => policy.RequireAuthenticatedUser().RequireRole(ApiRoleNames.Trainer))

            .AddPolicy(ApiPolicyNames.Client,
                policy => policy.RequireAuthenticatedUser().RequireRole(ApiRoleNames.Client))

            .AddPolicy(ApiPolicyNames.Superuser,
                policy => policy.RequireAuthenticatedUser().RequireRole(ApiRoleNames.Superuser))

            .AddPolicy(ApiPolicyNames.AdministrativeContext, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireRole(ApiRoleNames.Superuser);
                policy.AddRequirements(new AdministrativeContextRequirement());
            });

        services.AddSingleton<IAuthorizationHandler, AdministrativeContextAuthorizationHandler>();
        return services;
    }
}
