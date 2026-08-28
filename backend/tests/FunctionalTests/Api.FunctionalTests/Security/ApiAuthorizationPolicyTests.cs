using System.Security.Claims;
using Api.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Api.FunctionalTests.Security;

public sealed class ApiAuthorizationPolicyTests
{
    private static readonly IAuthorizationService AuthorizationService =
        new ServiceCollection()
            .AddLogging()
            .AddApiAuthorization()
            .BuildServiceProvider()
            .GetRequiredService<IAuthorizationService>();

    [Theory]
    [InlineData(ApiPolicyNames.Trainer, ApiRoleNames.Trainer, true)]
    [InlineData(ApiPolicyNames.Trainer, ApiRoleNames.Client, false)]
    [InlineData(ApiPolicyNames.Trainer, ApiRoleNames.Superuser, false)]
    [InlineData(ApiPolicyNames.Client, ApiRoleNames.Client, true)]
    [InlineData(ApiPolicyNames.Client, ApiRoleNames.Trainer, false)]
    [InlineData(ApiPolicyNames.Superuser, ApiRoleNames.Superuser, true)]
    [InlineData(ApiPolicyNames.Superuser, ApiRoleNames.Trainer, false)]
    [InlineData(ApiPolicyNames.Authenticated, ApiRoleNames.Client, true)]
    public async Task RolePolicies_AuthorizeOnlyTheIntendedRole(
        string policy,
        string role,
        bool expected)
    {
        var context = CreateContext(role);

        var result = await AuthorizationService.AuthorizeAsync(context.User, context, policy);

        Assert.Equal(expected, result.Succeeded);
    }

    [Theory]
    [InlineData(ApiPolicyNames.Authenticated)]
    [InlineData(ApiPolicyNames.Trainer)]
    [InlineData(ApiPolicyNames.Client)]
    [InlineData(ApiPolicyNames.Superuser)]
    [InlineData(ApiPolicyNames.AdministrativeContext)]
    public async Task EveryPolicy_RejectsAnonymousPrincipals(string policy)
    {
        var context = new DefaultHttpContext();

        var result = await AuthorizationService.AuthorizeAsync(context.User, context, policy);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task AdministrativeContext_AuthorizesSuperuserOnMarkedEndpoint()
    {
        var context = CreateContext(ApiRoleNames.Superuser, administrativeEndpoint: true);

        var result = await AuthorizationService.AuthorizeAsync(
            context.User,
            context,
            ApiPolicyNames.AdministrativeContext);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task AdministrativeContext_RejectsSuperuserOnEndpointWithoutMetadata()
    {
        var context = CreateContext(ApiRoleNames.Superuser);

        var result = await AuthorizationService.AuthorizeAsync(
            context.User,
            context,
            ApiPolicyNames.AdministrativeContext);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task AdministrativeContext_RejectsTrainerOnMarkedEndpoint()
    {
        var context = CreateContext(ApiRoleNames.Trainer, administrativeEndpoint: true);

        var result = await AuthorizationService.AuthorizeAsync(
            context.User,
            context,
            ApiPolicyNames.AdministrativeContext);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void ApiRoleNames_SupportsOnlyTheContractRoles()
    {
        Assert.True(ApiRoleNames.IsSupported(ApiRoleNames.Superuser));
        Assert.True(ApiRoleNames.IsSupported(ApiRoleNames.Trainer));
        Assert.True(ApiRoleNames.IsSupported(ApiRoleNames.Client));
        Assert.False(ApiRoleNames.IsSupported("Trainer"));
        Assert.False(ApiRoleNames.IsSupported("admin"));
    }

    private static DefaultHttpContext CreateContext(
        string role,
        bool administrativeEndpoint = false)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ApiClaimNames.Subject, Guid.NewGuid().ToString()),
                new Claim(ApiClaimNames.Role, role)
            ],
            "TestAuthentication",
            ApiClaimNames.Subject,
            ApiClaimNames.Role);
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };

        if (administrativeEndpoint)
            context.SetEndpoint(new Endpoint(
                _ => Task.CompletedTask,
                new EndpointMetadataCollection(new AdministrativeContextAttribute()),
                "administrative_test"));

        return context;
    }
}
