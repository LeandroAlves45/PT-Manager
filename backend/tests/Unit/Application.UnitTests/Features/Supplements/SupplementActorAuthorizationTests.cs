using Application.Common.Abstractions;
using Application.Features.Supplements;

namespace Application.UnitTests.Features.Supplements;

public sealed class SupplementActorAuthorizationTests
{
    [Fact]
    public void RequireTrainer_WithCompleteTrainerContext_Succeeds()
    {
        var context = Context("trainer", Guid.NewGuid(), Guid.NewGuid());

        var result = SupplementActorAuthorization.RequireTrainer(context);

        Assert.True(result.IsSuccess);
        Assert.Equal(context.TrainerId, result.Value.TrainerId);
    }

    [Theory]
    [InlineData("trainer", false, true, "tenant_required")]
    [InlineData("client", true, true, "supplement_trainer_only")]
    [InlineData("trainer", true, false, "unauthenticated_user")]
    public void RequireTrainer_WithInvalidContext_FailsClosed(
        string? role, bool hasTenant, bool hasUser, string expectedCode)
    {
        var context = Context(role, hasTenant ? Guid.NewGuid() : null,
            hasUser ? Guid.NewGuid() : null);

        var result = SupplementActorAuthorization.RequireTrainer(context);

        Assert.Equal(expectedCode, result.Error!.Code);
    }

    [Theory]
    [InlineData("client", true, true, "success")]
    [InlineData("trainer", true, true, "supplement_client_only")]
    [InlineData("client", false, true, "tenant_required")]
    [InlineData("client", true, false, "unauthenticated_user")]
    public void RequireClient_CoversIdentityMatrix(
        string role, bool hasTenant, bool hasUser, string expectedCode)
    {
        var context = Context(role, hasTenant ? Guid.NewGuid() : null,
            hasUser ? Guid.NewGuid() : null);

        var result = SupplementActorAuthorization.RequireClient(context);

        Assert.Equal(expectedCode,
            result.IsSuccess ? "success" : result.Error!.Code);
    }

    [Theory]
    [InlineData("superuser", true, true, "success")]
    [InlineData("superuser", false, true, "supplement_administrator_only")]
    [InlineData("trainer", true, true, "supplement_administrator_only")]
    [InlineData("superuser", true, false, "unauthenticated_user")]
    public void RequireAdministrator_CoversRolePolicyAndIdentity(
        string role, bool isAdministrative, bool hasUser, string expectedCode)
    {
        var context = Context(role, null, hasUser ? Guid.NewGuid() : null,
            isAdministrative);

        var result = SupplementActorAuthorization.RequireAdministrator(context);

        Assert.Equal(expectedCode,
            result.IsSuccess ? "success" : result.Error!.Code);
    }

    private static TestTenantContext Context(
        string? role, Guid? trainerId, Guid? userId, bool administrative = false) =>
        new(trainerId, userId, role, administrative);

    private sealed record TestTenantContext(
        Guid? TrainerId, Guid? UserId, string? Role, bool IsAdministrative) : ITenantContext
    {
        public TenantOrigin Origin => TenantOrigin.Http;
    }
}
