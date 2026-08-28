using System.Security.Claims;
using Api.Authorization;
using Api.FunctionalTests.Support;
using Api.Middlewares;
using Application.Common.Abstractions;
using Microsoft.AspNetCore.Http;

namespace Api.FunctionalTests.Security;

public sealed class TenantContextMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_IgnoresTenantHeadersAndUsesValidatedClaims()
    {
        var trainerId = Guid.NewGuid();
        var hostileTenantId = Guid.NewGuid();
        var context = CreateAuthenticatedContext(trainerId, trainerId, ApiRoleNames.Trainer);
        context.Request.Headers["X-Tenant-ID"] = hostileTenantId.ToString();
        var initializer = new RecordingTenantContextInitializer();
        var middleware = new TenantContextMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context, initializer, new StubAuthorizationService(false));

        Assert.True(initializer.WasEstablished);
        Assert.Equal(trainerId, initializer.TrainerId);
        Assert.NotEqual(hostileTenantId, initializer.TrainerId);
        Assert.False(initializer.IsAdministrative);
    }

    [Fact]
    public async Task InvokeAsync_EstablishesTheHttpOriginForAuthenticatedRequests()
    {
        var trainerId = Guid.NewGuid();
        var context = CreateAuthenticatedContext(trainerId, trainerId, ApiRoleNames.Trainer);
        var initializer = new RecordingTenantContextInitializer();
        var middleware = new TenantContextMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context, initializer, new StubAuthorizationService(false));

        Assert.Equal(TenantOrigin.Http, initializer.Origin);
        Assert.Equal(ApiRoleNames.Trainer, initializer.Role);
        Assert.Equal(trainerId, initializer.UserId);
    }

    [Fact]
    public async Task InvokeAsync_DoesNotEstablishContextForAnonymousRequest()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Tenant-ID"] = Guid.NewGuid().ToString();
        var initializer = new RecordingTenantContextInitializer();
        var middleware = new TenantContextMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context, initializer, new StubAuthorizationService(true));

        Assert.False(initializer.WasEstablished);
    }

    [Fact]
    public async Task InvokeAsync_ForwardsAnonymousRequestsToTheRestOfThePipeline()
    {
        var wasCalled = false;
        var middleware = new TenantContextMiddleware(_ =>
        {
            wasCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(
            new DefaultHttpContext(),
            new RecordingTenantContextInitializer(),
            new StubAuthorizationService(false));

        Assert.True(wasCalled);
    }

    [Fact]
    public async Task InvokeAsync_EstablishesAdministrativeContextOnlyWithMetadataAndPolicy()
    {
        var context = CreateAuthenticatedContext(Guid.NewGuid(), null, ApiRoleNames.Superuser);
        MarkAsAdministrativeEndpoint(context);
        var initializer = new RecordingTenantContextInitializer();
        var middleware = new TenantContextMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context, initializer, new StubAuthorizationService(true));

        Assert.True(initializer.IsAdministrative);
        Assert.Null(initializer.TrainerId);
    }

    [Fact]
    public async Task InvokeAsync_DoesNotEstablishAdministrativeContextWithoutMetadata()
    {
        var context = CreateAuthenticatedContext(Guid.NewGuid(), null, ApiRoleNames.Superuser);
        var initializer = new RecordingTenantContextInitializer();
        var middleware = new TenantContextMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context, initializer, new StubAuthorizationService(true));

        Assert.False(initializer.IsAdministrative);
    }

    [Fact]
    public async Task InvokeAsync_DoesNotEstablishAdministrativeContextWhenPolicyFails()
    {
        var context = CreateAuthenticatedContext(Guid.NewGuid(), null, ApiRoleNames.Superuser);
        MarkAsAdministrativeEndpoint(context);
        var initializer = new RecordingTenantContextInitializer();
        var middleware = new TenantContextMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context, initializer, new StubAuthorizationService(false));

        Assert.False(initializer.IsAdministrative);
    }

    [Fact]
    public async Task InvokeAsync_RejectsTrainerWhoseTenantDiffersFromSubject()
    {
        var context = CreateAuthenticatedContext(
            Guid.NewGuid(),
            Guid.NewGuid(),
            ApiRoleNames.Trainer);
        var middleware = new TenantContextMiddleware(_ => Task.CompletedTask);

        await Assert.ThrowsAsync<InvalidAuthenticatedPrincipalException>(() =>
            middleware.InvokeAsync(
                context,
                new RecordingTenantContextInitializer(),
                new StubAuthorizationService(false)));
    }

    [Fact]
    public async Task InvokeAsync_AcceptsClientBelongingToAnotherTrainer()
    {
        var clientUserId = Guid.NewGuid();
        var trainerId = Guid.NewGuid();
        var context = CreateAuthenticatedContext(clientUserId, trainerId, ApiRoleNames.Client);
        var initializer = new RecordingTenantContextInitializer();
        var middleware = new TenantContextMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context, initializer, new StubAuthorizationService(false));

        Assert.Equal(trainerId, initializer.TrainerId);
        Assert.Equal(clientUserId, initializer.UserId);
    }

    [Fact]
    public async Task InvokeAsync_RejectsClientWithoutTenantClaim()
    {
        var context = CreateAuthenticatedContext(Guid.NewGuid(), null, ApiRoleNames.Client);
        var middleware = new TenantContextMiddleware(_ => Task.CompletedTask);

        await Assert.ThrowsAsync<InvalidAuthenticatedPrincipalException>(() =>
            middleware.InvokeAsync(
                context,
                new RecordingTenantContextInitializer(),
                new StubAuthorizationService(false)));
    }

    [Fact]
    public async Task InvokeAsync_RejectsUnsupportedRole()
    {
        var userId = Guid.NewGuid();
        var context = CreateAuthenticatedContext(userId, userId, "admin");
        var middleware = new TenantContextMiddleware(_ => Task.CompletedTask);

        await Assert.ThrowsAsync<InvalidAuthenticatedPrincipalException>(() =>
            middleware.InvokeAsync(
                context,
                new RecordingTenantContextInitializer(),
                new StubAuthorizationService(false)));
    }

    [Fact]
    public async Task InvokeAsync_RejectsDuplicatedRoleClaims()
    {
        var userId = Guid.NewGuid();
        var context = CreateAuthenticatedContext(userId, userId, ApiRoleNames.Trainer);
        context.User.Identities.Single()
            .AddClaim(new Claim(ApiClaimNames.Role, ApiRoleNames.Superuser));
        var middleware = new TenantContextMiddleware(_ => Task.CompletedTask);

        await Assert.ThrowsAsync<InvalidAuthenticatedPrincipalException>(() =>
            middleware.InvokeAsync(
                context,
                new RecordingTenantContextInitializer(),
                new StubAuthorizationService(false)));
    }

    [Fact]
    public async Task InvokeAsync_RejectsSubjectThatIsNotAGuid()
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ApiClaimNames.Subject, "not-a-guid"),
                new Claim(ApiClaimNames.Role, ApiRoleNames.Superuser)
            ],
            "TestAuthentication",
            ApiClaimNames.Subject,
            ApiClaimNames.Role);
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        var middleware = new TenantContextMiddleware(_ => Task.CompletedTask);

        await Assert.ThrowsAsync<InvalidAuthenticatedPrincipalException>(() =>
            middleware.InvokeAsync(
                context,
                new RecordingTenantContextInitializer(),
                new StubAuthorizationService(false)));
    }

    [Fact]
    public async Task InvokeAsync_DoesNotReachThePipelineWhenClaimsAreInvalid()
    {
        var wasCalled = false;
        var context = CreateAuthenticatedContext(
            Guid.NewGuid(),
            Guid.NewGuid(),
            ApiRoleNames.Trainer);
        var middleware = new TenantContextMiddleware(_ =>
        {
            wasCalled = true;
            return Task.CompletedTask;
        });

        await Assert.ThrowsAsync<InvalidAuthenticatedPrincipalException>(() =>
            middleware.InvokeAsync(
                context,
                new RecordingTenantContextInitializer(),
                new StubAuthorizationService(false)));

        Assert.False(wasCalled);
    }

    private static void MarkAsAdministrativeEndpoint(HttpContext context) =>
        context.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(new AdministrativeContextAttribute()),
            "administrative_test"));

    private static DefaultHttpContext CreateAuthenticatedContext(
        Guid userId,
        Guid? trainerId,
        string role)
    {
        var claims = new List<Claim>
        {
            new(ApiClaimNames.Subject, userId.ToString()),
            new(ApiClaimNames.Role, role)
        };

        if (trainerId.HasValue)
            claims.Add(new Claim(ApiClaimNames.TrainerId, trainerId.Value.ToString()));

        var identity = new ClaimsIdentity(
            claims,
            "TestAuthentication",
            ApiClaimNames.Subject,
            ApiClaimNames.Role);
        return new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
    }
}
