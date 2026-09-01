using Api.Authorization;
using Api.Configuration;
using Api.Contracts.Administration;
using Api.Controllers;
using Application.Common.Abstractions;
using Application.Features.Administration.ContentModeration.Abstractions;
using Application.Features.Administration.ContentModeration.BlockExercise;
using Application.Features.Administration.ContentModeration.BlockFood;
using Application.Features.Administration.ContentModeration.UnblockFood;
using Domain.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Api.FunctionalTests.Controllers;

public sealed class AdminContentModerationControllerTests
{
    [Fact]
    public async Task BlockFood_WhenChanged_ReturnsNoContent()
    {
        var controller = CreateController();
        var handler = CreateHandler(PrivateCatalogModerationStoreResult.Changed);

        var response = await controller.BlockFoodAsync(Guid.NewGuid(),
            new BlockContentRequest("malicious_content"), handler,
            TestContext.Current.CancellationToken);

        Assert.IsType<NoContentResult>(response);
    }

    [Fact]
    public async Task BlockFood_WhenAlreadyBlocked_ReturnsNoContentIdempotently()
    {
        var controller = CreateController();
        var handler = CreateHandler(PrivateCatalogModerationStoreResult.AlreadyInRequestedState);

        var response = await controller.BlockFoodAsync(Guid.NewGuid(),
            new BlockContentRequest("malicious_content"), handler,
            TestContext.Current.CancellationToken);

        Assert.IsType<NoContentResult>(response);
    }

    [Fact]
    public async Task BlockFood_WhenGlobal_ReturnsConflictProblemDetails()
    {
        var controller = CreateController();
        var handler = CreateHandler(PrivateCatalogModerationStoreResult.NotPrivate);

        var response = await controller.BlockFoodAsync(Guid.NewGuid(),
            new BlockContentRequest("prohibited_content"), handler,
            TestContext.Current.CancellationToken);

        var objectResult = Assert.IsType<ObjectResult>(response);
        Assert.Equal(StatusCodes.Status409Conflict, objectResult.StatusCode);
    }

    [Fact]
    public async Task BlockFood_WhenMissing_ReturnsNotFoundProblemDetails()
    {
        var controller = CreateController();
        var handler = CreateHandler(PrivateCatalogModerationStoreResult.NotFound);

        var response = await controller.BlockFoodAsync(Guid.NewGuid(),
            new BlockContentRequest("prohibited_content"), handler,
            TestContext.Current.CancellationToken);

        var objectResult = Assert.IsType<ObjectResult>(response);
        Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);
    }

    [Fact]
    public async Task BlockFood_WhenActorRejectedByStore_ReturnsForbiddenProblemDetails()
    {
        var controller = CreateController();
        var handler = CreateHandler(PrivateCatalogModerationStoreResult.ActorInvalid);

        var response = await controller.BlockFoodAsync(Guid.NewGuid(),
            new BlockContentRequest("prohibited_content"), handler,
            TestContext.Current.CancellationToken);

        var objectResult = Assert.IsType<ObjectResult>(response);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
    }

    [Fact]
    public async Task BlockFood_WithUnknownReason_ReturnsBadRequestProblemDetails()
    {
        var controller = CreateController();
        var handler = CreateHandler(PrivateCatalogModerationStoreResult.Changed);

        var response = await controller.BlockFoodAsync(Guid.NewGuid(),
            new BlockContentRequest("free_text"), handler,
            TestContext.Current.CancellationToken);

        var objectResult = Assert.IsType<ObjectResult>(response);
        Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
    }

    [Fact]
    public async Task BlockFood_ProblemDetails_ExposeStableErrorDetail()
    {
        var controller = CreateController();
        var handler = CreateHandler(PrivateCatalogModerationStoreResult.NotPrivate);

        var response = await controller.BlockFoodAsync(Guid.NewGuid(),
            new BlockContentRequest("prohibited_content"), handler,
            TestContext.Current.CancellationToken);

        var problem = Assert.IsType<ProblemDetails>(
            Assert.IsType<ObjectResult>(response).Value);
        Assert.Equal(
            "Only private catalog resources can be moderated by this operation.",
            problem.Detail);
    }

    [Fact]
    public async Task UnblockFood_WhenChanged_ReturnsNoContent()
    {
        var controller = CreateController();
        var handler = new UnblockFoodHandler(
            new TestTenantContext(), new TestClock(),
            new StubStore(PrivateCatalogModerationStoreResult.Changed));

        var response = await controller.UnblockFoodAsync(Guid.NewGuid(), handler,
            TestContext.Current.CancellationToken);

        Assert.IsType<NoContentResult>(response);
    }

    [Fact]
    public async Task BlockExercise_WhenChanged_ReturnsNoContent()
    {
        var controller = CreateController();
        var handler = new BlockExerciseHandler(new BlockExerciseCommandValidator(),
            new TestTenantContext(), new TestClock(),
            new StubStore(PrivateCatalogModerationStoreResult.Changed));

        var response = await controller.BlockExerciseAsync(Guid.NewGuid(),
            new BlockContentRequest("dangerous_information"), handler,
            TestContext.Current.CancellationToken);

        Assert.IsType<NoContentResult>(response);
    }

    [Fact]
    public void Controller_RequiresAdministrativePolicyMetadataAndRateLimit()
    {
        var type = typeof(AdminContentModerationController);
        var policy = type.GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>().Single().Policy;
        var hasAdministrativeContext = type.IsDefined(typeof(AdministrativeContextAttribute), true);
        var ratePolicy = type.GetCustomAttributes(typeof(EnableRateLimitingAttribute), true)
            .Cast<EnableRateLimitingAttribute>().Single().PolicyName;

        Assert.Equal(
            (ApiPolicyNames.AdministrativeContext, true, ApiRateLimitPolicyNames.Moderation),
            (policy, hasAdministrativeContext, ratePolicy));
    }

    [Fact]
    public void Controller_ExposesFourModerationRoutes()
    {
        var routes = typeof(AdminContentModerationController)
            .GetMethods()
            .SelectMany(method => method.GetCustomAttributes(typeof(HttpPostAttribute), true)
                .Cast<HttpPostAttribute>())
            .Select(attribute => attribute.Template)
            .OrderBy(template => template, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[]
            {
                "exercises/{exerciseId:guid}/block",
                "exercises/{exerciseId:guid}/unblock",
                "foods/{foodId:guid}/block",
                "foods/{foodId:guid}/unblock"
            },
            routes);
    }

    private static AdminContentModerationController CreateController() => new()
    {
        ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        }
    };

    private static BlockFoodHandler CreateHandler(PrivateCatalogModerationStoreResult outcome) =>
        new(new BlockFoodCommandValidator(), new TestTenantContext(), new TestClock(), new StubStore(outcome));

    private sealed class StubStore(PrivateCatalogModerationStoreResult outcome) : IPrivateCatalogModerationStore
    {
        public Task<PrivateCatalogModerationStoreResult> BlockFoodAsync(Guid actorUserId, Guid foodId, PlatformEnforcementReason reason, DateTime now, CancellationToken cancellationToken) => Task.FromResult(outcome);
        public Task<PrivateCatalogModerationStoreResult> UnblockFoodAsync(Guid actorUserId, Guid foodId, DateTime now, CancellationToken cancellationToken) => Task.FromResult(outcome);
        public Task<PrivateCatalogModerationStoreResult> BlockExerciseAsync(Guid actorUserId, Guid exerciseId, PlatformEnforcementReason reason, DateTime now, CancellationToken cancellationToken) => Task.FromResult(outcome);
        public Task<PrivateCatalogModerationStoreResult> UnblockExerciseAsync(Guid actorUserId, Guid exerciseId, DateTime now, CancellationToken cancellationToken) => Task.FromResult(outcome);
    }

    private sealed class TestClock : IClock
    {
        public DateTime UtcNow => new(2026, 8, 31, 12, 0, 0, DateTimeKind.Utc);
    }

    private sealed class TestTenantContext : ITenantContext
    {
        public Guid? TrainerId => null;
        public Guid? UserId => Guid.Parse("11111111-1111-1111-1111-111111111111");
        public string? Role => "superuser";
        public TenantOrigin Origin => TenantOrigin.Http;
        public bool IsAdministrative => true;
    }
}
