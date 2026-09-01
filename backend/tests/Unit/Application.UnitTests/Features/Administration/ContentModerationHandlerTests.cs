using Application.Common.Abstractions;
using Application.Features.Administration.ContentModeration.Abstractions;
using Application.Features.Administration.ContentModeration.BlockExercise;
using Application.Features.Administration.ContentModeration.BlockFood;
using Application.Features.Administration.ContentModeration.UnblockExercise;
using Application.Features.Administration.ContentModeration.UnblockFood;
using Domain.ValueObjects;

namespace Application.UnitTests.Features.Administration;

public sealed class ContentModerationHandlerTests
{
    private static readonly Guid ActorId = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 8, 31, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task BlockFood_WithValidAdministrator_PropagatesStructuredReason()
    {
        var store = new RecordingStore();
        var handler = new BlockFoodHandler(new BlockFoodCommandValidator(),
            new TestTenantContext("superuser", true), new TestClock(), store);

        var result = await handler.HandleAsync(
            new BlockFoodCommand(Guid.NewGuid(), "dangerous_information"),
            TestContext.Current.CancellationToken);

        Assert.Equal(
            (true, PlatformEnforcementReason.DangerousInformation),
            (result.IsSuccess, store.LastReason));
    }

    [Fact]
    public async Task BlockFood_WithValidAdministrator_PropagatesActorAndResourceId()
    {
        var store = new RecordingStore();
        var foodId = Guid.NewGuid();
        var handler = new BlockFoodHandler(new BlockFoodCommandValidator(),
            new TestTenantContext("superuser", true), new TestClock(), store);

        await handler.HandleAsync(
            new BlockFoodCommand(foodId, "malicious_content"),
            TestContext.Current.CancellationToken);

        Assert.Equal((ActorId, foodId, Now), (store.LastActorId, store.LastResourceId, store.LastNow));
    }

    [Fact]
    public async Task BlockFood_WithValidAdministrator_PropagatesCancellationToken()
    {
        var store = new RecordingStore();
        using var cancellation = new CancellationTokenSource();
        var handler = new BlockFoodHandler(new BlockFoodCommandValidator(),
            new TestTenantContext("superuser", true), new TestClock(), store);

        await handler.HandleAsync(
            new BlockFoodCommand(Guid.NewGuid(), "malicious_content"), cancellation.Token);

        Assert.Equal(cancellation.Token, store.LastToken);
    }

    [Fact]
    public async Task BlockExercise_WithoutAdministrativeContext_ReturnsForbidden()
    {
        var handler = new BlockExerciseHandler(new BlockExerciseCommandValidator(),
            new TestTenantContext("superuser", false), new TestClock(), new RecordingStore());

        var result = await handler.HandleAsync(
            new BlockExerciseCommand(Guid.NewGuid(), "malicious_content"),
            TestContext.Current.CancellationToken);

        Assert.Equal("content_moderation_administrator_only", result.Error?.Code);
    }

    [Fact]
    public async Task BlockExercise_WithTrainerRole_ReturnsForbidden()
    {
        var handler = new BlockExerciseHandler(new BlockExerciseCommandValidator(),
            new TestTenantContext("trainer", true), new TestClock(), new RecordingStore());

        var result = await handler.HandleAsync(
            new BlockExerciseCommand(Guid.NewGuid(), "malicious_content"),
            TestContext.Current.CancellationToken);

        Assert.Equal("content_moderation_administrator_only", result.Error?.Code);
    }

    [Fact]
    public async Task BlockExercise_WithoutAdministrativeContext_DoesNotReachStore()
    {
        var store = new RecordingStore();
        var handler = new BlockExerciseHandler(new BlockExerciseCommandValidator(),
            new TestTenantContext("superuser", false), new TestClock(), store);

        await handler.HandleAsync(
            new BlockExerciseCommand(Guid.NewGuid(), "malicious_content"),
            TestContext.Current.CancellationToken);

        Assert.Equal(0, store.Calls);
    }

    [Fact]
    public async Task BlockFood_WithUnknownReason_ReturnsValidationFailure()
    {
        var handler = new BlockFoodHandler(new BlockFoodCommandValidator(),
            new TestTenantContext("superuser", true), new TestClock(), new RecordingStore());

        var result = await handler.HandleAsync(
            new BlockFoodCommand(Guid.NewGuid(), "free_text"),
            TestContext.Current.CancellationToken);

        Assert.Equal("platform_enforcement_reason_invalid", result.Error?.ValidationErrors.Single().Code);
    }

    [Fact]
    public async Task BlockFood_WithEmptyId_ReturnsValidationFailure()
    {
        var handler = new BlockFoodHandler(new BlockFoodCommandValidator(),
            new TestTenantContext("superuser", true), new TestClock(), new RecordingStore());

        var result = await handler.HandleAsync(
            new BlockFoodCommand(Guid.Empty, "malicious_content"),
            TestContext.Current.CancellationToken);

        Assert.Contains(result.Error!.ValidationErrors, error => error.Code == "food_id_required");
    }

    [Fact]
    public async Task BlockFood_WithInvalidCommand_DoesNotReachStore()
    {
        var store = new RecordingStore();
        var handler = new BlockFoodHandler(new BlockFoodCommandValidator(),
            new TestTenantContext("superuser", true), new TestClock(), store);

        await handler.HandleAsync(
            new BlockFoodCommand(Guid.NewGuid(), "free_text"),
            TestContext.Current.CancellationToken);

        Assert.Equal(0, store.Calls);
    }

    [Fact]
    public async Task UnblockFood_WhenAlreadyAllowed_IsIdempotentSuccess()
    {
        var handler = new UnblockFoodHandler(
            new TestTenantContext("superuser", true), new TestClock(), new RecordingStore());

        var result = await handler.HandleAsync(
            new UnblockFoodCommand(Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task UnblockExercise_WithoutAdministrativeContext_ReturnsForbidden()
    {
        var handler = new UnblockExerciseHandler(
            new TestTenantContext("superuser", false), new TestClock(), new RecordingStore());

        var result = await handler.HandleAsync(
            new UnblockExerciseCommand(Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        Assert.Equal("content_moderation_administrator_only", result.Error?.Code);
    }

    [Fact]
    public async Task BlockFood_WhenResourceIsGlobal_MapsToConflict()
    {
        var handler = new BlockFoodHandler(new BlockFoodCommandValidator(),
            new TestTenantContext("superuser", true), new TestClock(),
            new RecordingStore(PrivateCatalogModerationStoreResult.NotPrivate));

        var result = await handler.HandleAsync(
            new BlockFoodCommand(Guid.NewGuid(), "malicious_content"),
            TestContext.Current.CancellationToken);

        Assert.Equal("content_moderation_resource_not_private", result.Error?.Code);
    }

    [Fact]
    public async Task BlockFood_WhenResourceMissing_MapsToNotFound()
    {
        var handler = new BlockFoodHandler(new BlockFoodCommandValidator(),
            new TestTenantContext("superuser", true), new TestClock(),
            new RecordingStore(PrivateCatalogModerationStoreResult.NotFound));

        var result = await handler.HandleAsync(
            new BlockFoodCommand(Guid.NewGuid(), "malicious_content"),
            TestContext.Current.CancellationToken);

        Assert.Equal("content_moderation_resource_not_found", result.Error?.Code);
    }

    [Fact]
    public async Task BlockFood_WhenStoreRejectsActor_MapsToForbidden()
    {
        var handler = new BlockFoodHandler(new BlockFoodCommandValidator(),
            new TestTenantContext("superuser", true), new TestClock(),
            new RecordingStore(PrivateCatalogModerationStoreResult.ActorInvalid));

        var result = await handler.HandleAsync(
            new BlockFoodCommand(Guid.NewGuid(), "malicious_content"),
            TestContext.Current.CancellationToken);

        Assert.Equal("content_moderation_administrator_only", result.Error?.Code);
    }

    private sealed class RecordingStore : IPrivateCatalogModerationStore
    {
        private readonly PrivateCatalogModerationStoreResult _blockOutcome;

        public RecordingStore(
            PrivateCatalogModerationStoreResult blockOutcome =
                PrivateCatalogModerationStoreResult.Changed) =>
            _blockOutcome = blockOutcome;

        public PlatformEnforcementReason? LastReason { get; private set; }
        public Guid LastActorId { get; private set; }
        public Guid LastResourceId { get; private set; }
        public DateTime LastNow { get; private set; }
        public CancellationToken LastToken { get; private set; }
        public int Calls { get; private set; }

        public Task<PrivateCatalogModerationStoreResult> BlockFoodAsync(Guid actorUserId, Guid foodId, PlatformEnforcementReason reason, DateTime now, CancellationToken cancellationToken)
        {
            Record(actorUserId, foodId, now, cancellationToken);
            LastReason = reason;
            return Task.FromResult(_blockOutcome);
        }

        public Task<PrivateCatalogModerationStoreResult> UnblockFoodAsync(Guid actorUserId, Guid foodId, DateTime now, CancellationToken cancellationToken)
        {
            Record(actorUserId, foodId, now, cancellationToken);
            return Task.FromResult(PrivateCatalogModerationStoreResult.AlreadyInRequestedState);
        }

        public Task<PrivateCatalogModerationStoreResult> BlockExerciseAsync(Guid actorUserId, Guid exerciseId, PlatformEnforcementReason reason, DateTime now, CancellationToken cancellationToken)
        {
            Record(actorUserId, exerciseId, now, cancellationToken);
            LastReason = reason;
            return Task.FromResult(_blockOutcome);
        }

        public Task<PrivateCatalogModerationStoreResult> UnblockExerciseAsync(Guid actorUserId, Guid exerciseId, DateTime now, CancellationToken cancellationToken)
        {
            Record(actorUserId, exerciseId, now, cancellationToken);
            return Task.FromResult(PrivateCatalogModerationStoreResult.AlreadyInRequestedState);
        }

        private void Record(Guid actorUserId, Guid resourceId, DateTime now, CancellationToken cancellationToken)
        {
            Calls++;
            LastActorId = actorUserId;
            LastResourceId = resourceId;
            LastNow = now;
            LastToken = cancellationToken;
        }
    }

    private sealed class TestClock : IClock
    {
        public DateTime UtcNow => Now;
    }

    private sealed class TestTenantContext(string? role, bool isAdministrative) : ITenantContext
    {
        public Guid? TrainerId => null;
        public Guid? UserId => ActorId;
        public string? Role => role;
        public TenantOrigin Origin => TenantOrigin.Http;
        public bool IsAdministrative => isAdministrative;
    }
}
