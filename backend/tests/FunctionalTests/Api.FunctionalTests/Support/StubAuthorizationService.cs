using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace Api.FunctionalTests.Support;

/// <summary>Devolve um resultado fixo para isolar o middleware da avaliação de policies.</summary>
internal sealed class StubAuthorizationService : IAuthorizationService
{
    private readonly bool _succeeds;

    public StubAuthorizationService(bool succeeds) => _succeeds = succeeds;

    public Task<AuthorizationResult> AuthorizeAsync(
        ClaimsPrincipal user,
        object? resource,
        IEnumerable<IAuthorizationRequirement> requirements) =>
        Task.FromResult(_succeeds
            ? AuthorizationResult.Success()
            : AuthorizationResult.Failed());

    public Task<AuthorizationResult> AuthorizeAsync(
        ClaimsPrincipal user,
        object? resource,
        string policyName) =>
        Task.FromResult(_succeeds
            ? AuthorizationResult.Success()
            : AuthorizationResult.Failed());
}
