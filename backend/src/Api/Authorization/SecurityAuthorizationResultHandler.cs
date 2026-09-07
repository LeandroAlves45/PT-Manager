using Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace Api.Authorization;

/// <summary>Regista recusas de autenticação e autorização antes de produzir a resposta HTTP.</summary>
public sealed class SecurityAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _defaultHandler = new();
    private readonly ILogger<SecurityAuthorizationResultHandler> _logger;

    public SecurityAuthorizationResultHandler(ILogger<SecurityAuthorizationResultHandler> logger) =>
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Challenged || authorizeResult.Forbidden)
        {
            var authorizeData = context.GetEndpoint()?.Metadata.GetOrderedMetadata<IAuthorizeData>()
                ?? [];
            var policies = authorizeData
                .Select(data => data.Policy)
                .Where(value => !string.IsNullOrWhiteSpace(value));
            var roles = authorizeData
                .Select(data => data.Roles)
                .Where(value => !string.IsNullOrWhiteSpace(value));
            var policyNames = policies.ToArray();
            var requiredRoles = roles.ToArray();
            var isRolePolicy = policyNames.Any(name => name is
                ApiPolicyNames.Trainer or ApiPolicyNames.Client or ApiPolicyNames.Superuser);
            var rejectionCategory = authorizeResult.Challenged
                ? "authentication"
                : requiredRoles.Length > 0 || isRolePolicy ? "role" : "policy";

            _logger.LogWarning(SecurityLogEvents.AuthorizationRejection,
                "Request authorization was rejected with category {AuthorizationRejectionCategory}, policies {AuthorizationPolicies}, roles {RequiredRoles}, and path {RequestPath}.",
                rejectionCategory,
                string.Join(',', policyNames),
                string.Join(',', requiredRoles),
                context.Request.Path.Value);
        }

        await _defaultHandler.HandleAsync(next, context, policy, authorizeResult);
    }
}
