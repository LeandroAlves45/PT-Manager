using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Errors;

namespace Application.UnitTests.Common.Authorization;

public sealed class ActorAuthorizationTests
{
    private static readonly Error RoleError = Error.Create(
        "test_role_only", ErrorCategory.Forbidden, "Role mismatch.");

    [Fact]
    public void RequireTrainer_WhenTenantIsMissing_ReturnsTenantRequired()
    {
        var context = new StubTenantContext(null, Guid.NewGuid(), "trainer", false);

        var result = ActorAuthorization.RequireTrainer(context, RoleError);

        Assert.Equal("tenant_required", result.Error!.Code);
    }

    [Fact]
    public void RequireTrainer_WhenRoleIsWrong_ReturnsFeatureRoleError()
    {
        var context = new StubTenantContext(Guid.NewGuid(), Guid.NewGuid(), "client", false);

        var result = ActorAuthorization.RequireTrainer(context, RoleError);

        Assert.Equal(RoleError.Code, result.Error!.Code);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RequireTrainer_WhenUserIsNotAuthenticated_ReturnsUnauthenticated(bool useEmptyId)
    {
        Guid? userId = useEmptyId ? Guid.Empty : null;
        var context = new StubTenantContext(Guid.NewGuid(), userId, "trainer", false);

        var result = ActorAuthorization.RequireTrainer(context, RoleError);

        Assert.Equal("unauthenticated_user", result.Error!.Code);
    }

    [Fact]
    public void RequireClient_WhenValid_ReturnsResolvedActor()
    {
        var trainerId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var context = new StubTenantContext(trainerId, userId, "client", false);

        var result = ActorAuthorization.RequireClient(context, RoleError);

        Assert.Equal((trainerId, userId), (result.Value.TrainerId, result.Value.UserId));
    }

    [Theory]
    [InlineData("trainer", true)]
    [InlineData("superuser", false)]
    public void RequireAdministrator_WhenPolicyIsIncomplete_ReturnsFeatureRoleError(
        string role, bool isAdministrative)
    {
        var context = new StubTenantContext(null, Guid.NewGuid(), role, isAdministrative);

        var result = ActorAuthorization.RequireAdministrator(context, RoleError);

        Assert.Equal(RoleError.Code, result.Error!.Code);
    }

    [Fact]
    public void RequireAdministrator_WhenValid_DoesNotRequireTenant()
    {
        var userId = Guid.NewGuid();
        var context = new StubTenantContext(null, userId, "superuser", true);

        var result = ActorAuthorization.RequireAdministrator(context, RoleError);

        Assert.Equal(userId, result.Value.UserId);
    }

    private sealed class StubTenantContext(
        Guid? trainerId, Guid? userId, string? role, bool isAdministrative) : ITenantContext
    {
        public Guid? TrainerId => trainerId;
        public Guid? UserId => userId;
        public string? Role => role;
        public TenantOrigin Origin => TenantOrigin.Http;
        public bool IsAdministrative => isAdministrative;
    }
}
