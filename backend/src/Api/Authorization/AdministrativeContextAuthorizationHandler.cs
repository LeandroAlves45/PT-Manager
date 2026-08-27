using Microsoft.AspNetCore.Authorization;

namespace Api.Authorization;

/// <summary>Autoriza o bypass administrativo apenas no endpoint explicitamente marcado.</summary>
public sealed class AdministrativeContextAuthorizationHandler
    : AuthorizationHandler<AdministrativeContextRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AdministrativeContextRequirement requirement)
    {
        var httpContext = context.Resource as HttpContext;
        var endpointAllowsAdministration = httpContext?.GetEndpoint()
            ?.Metadata.GetMetadata<AdministrativeContextAttribute>() is not null;

        if (endpointAllowsAdministration &&
            context.User.Identity?.IsAuthenticated is true &&
            context.User.HasClaim(ApiClaimNames.Role, ApiRoleNames.Superuser))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
