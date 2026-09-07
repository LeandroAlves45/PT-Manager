using Api.Security;
using Microsoft.AspNetCore.Authorization;

namespace Api.Authorization;

/// <summary>Autoriza o bypass administrativo apenas no endpoint explicitamente marcado.</summary>
public sealed class AdministrativeContextAuthorizationHandler
    : AuthorizationHandler<AdministrativeContextRequirement>
{
    private const string AdministrativeContextLoggedItemKey =
        "Api.Security.AdministrativeContextLogged";
    private readonly ILogger<AdministrativeContextAuthorizationHandler> _logger;

    public AdministrativeContextAuthorizationHandler(
        ILogger<AdministrativeContextAuthorizationHandler> logger) =>
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

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
        {
            context.Succeed(requirement);
            if (httpContext!.Items.TryAdd(AdministrativeContextLoggedItemKey, true))
            {
                _logger.LogInformation(SecurityLogEvents.AdministrativeContext,
                    "Administrative context authorization completed with outcome {SecurityOutcome}.",
                    "succeeded");
            }
        }

        return Task.CompletedTask;
    }
}
