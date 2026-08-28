using System.Security.Claims;
using Api.Authorization;
using Application.Common.Abstractions;
using Microsoft.AspNetCore.Authorization;

namespace Api.Middlewares;

/// <summary>Compões tenant apenas a partir de claims autenticadas e policies validados.</summary>
public sealed class TenantContextMiddleware
{
    private readonly RequestDelegate _next;

    public TenantContextMiddleware(RequestDelegate next) =>
        _next = next ?? throw new ArgumentNullException(nameof(next));

    public async Task InvokeAsync(
        HttpContext httpContext,
        ITenantContextInitializer initializer,
        IAuthorizationService authorizationService)
    {
        if (httpContext.User.Identity?.IsAuthenticated is not true)
        {
            await _next(httpContext);
            return;
        }

        var userId = ParseRequiredGuidClaim(httpContext.User, ApiClaimNames.Subject);
        var role = ReadRequiredSingleClaim(httpContext.User, ApiClaimNames.Role);

        if (!ApiRoleNames.IsSupported(role))
            throw new InvalidAuthenticatedPrincipalException();

        var trainerId = ResolveTrainerId(httpContext.User, role, userId);
        var isAdministrative = await ResolveAdministrativeContextAsync(
            httpContext,
            authorizationService);

        initializer.Establish(
            trainerId,
            userId,
            role,
            TenantOrigin.Http,
            isAdministrative
        );

        await _next(httpContext);
    }

    private static Guid? ResolveTrainerId(
        ClaimsPrincipal principal,
        string role,
        Guid userId)
    {
        if (role == ApiRoleNames.Superuser)
            return null;

        var trainerId = ParseRequiredGuidClaim(principal, ApiClaimNames.TrainerId);
        if (role == ApiRoleNames.Trainer && trainerId != userId)
            throw new InvalidAuthenticatedPrincipalException();

        return trainerId;
    }

    private static async Task<bool> ResolveAdministrativeContextAsync(
        HttpContext httpContext,
        IAuthorizationService authorizationService)
    {
        if (httpContext.GetEndpoint()?.Metadata.GetMetadata<AdministrativeContextAttribute>() is null)
            return false;

        var result = await authorizationService.AuthorizeAsync(
            httpContext.User,
            httpContext,
            ApiPolicyNames.AdministrativeContext);

        return result.Succeeded;
    }

    private static Guid ParseRequiredGuidClaim(
        ClaimsPrincipal principal,
        string claimType)
    {
        var value = ReadRequiredSingleClaim(principal, claimType);
        return Guid.TryParse(value, out var parsed)
            ? parsed
            : throw new InvalidAuthenticatedPrincipalException();
    }

    private static string ReadRequiredSingleClaim(
        ClaimsPrincipal principal,
        string claimType)
    {
        var claims = principal.FindAll(claimType).ToArray();
        return claims.Length == 1 && !string.IsNullOrWhiteSpace(claims[0].Value)
            ? claims[0].Value
            : throw new InvalidAuthenticatedPrincipalException();
    }
}
