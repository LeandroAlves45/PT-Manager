using Application.Common.Abstractions;
using Application.Features.ClientPortal;
using Application.Features.ClientPortal.Abstractions;
using Application.Features.ClientPortal.Dtos;
using Application.Features.ClientPortal.RemoveMyAvatar;
using Application.Features.ClientPortal.ReplaceMyAvatar;
using Application.UnitTests.Common.Media;

namespace Application.UnitTests.Features.ClientPortal;

/// <summary>
/// Prova os pontos 2, 3 e 4 do Gate 5C para o avatar: só o próprio cliente o
/// altera, um veredicto não aprovado nunca publica, e falhas compensam o asset
/// novo sem tocar no anterior.
/// </summary>
public sealed class MyAvatarHandlerTests
{
    private static readonly DateTime Now = new(2026, 9, 11, 10, 0, 0, DateTimeKind.Utc);
    private static readonly Guid TrainerId = Guid.NewGuid();
    private static readonly Guid ClientUserId = Guid.NewGuid();

    [Fact]
    public async Task Replace_WhenActorIsTrainer_ReturnsForbiddenWithoutExternalIo()
    {
        var harness = new MediaHarness();
        var store = new FakeAvatarStore();

        var result = await CreateReplaceHandler(harness, store, role: "trainer").HandleAsync(
            ValidCommand(), TestContext.Current.CancellationToken);

        Assert.Equal(ClientPortalErrors.ClientOnly.Code, result.Error!.Code);
        Assert.Empty(harness.Journal.Calls);
        Assert.False(store.ReplaceWasCalled);
    }

    [Fact]
    public async Task Replace_UsesTheAuthenticatedIdentityOnly()
    {
        var harness = new MediaHarness();
        var store = new FakeAvatarStore();

        await CreateReplaceHandler(harness, store).HandleAsync(
            ValidCommand(), TestContext.Current.CancellationToken);

        Assert.Equal((TrainerId, ClientUserId), (store.TrainerId, store.ClientUserId));
        Assert.Equal(TrainerId, harness.Storage.UploadedRequest!.TrainerId);
    }

    [Theory]
    [InlineData(ImageModerationVerdict.Rejected, "portal_avatar_content_not_allowed")]
    [InlineData(ImageModerationVerdict.ReviewRequired, "portal_avatar_content_not_allowed")]
    public async Task Replace_WhenModerationDoesNotApprove_NeverPublishesOrPersists(
        ImageModerationVerdict verdict,
        string expectedCode)
    {
        var harness = new MediaHarness(verdict: verdict);
        var store = new FakeAvatarStore();

        var result = await CreateReplaceHandler(harness, store).HandleAsync(
            ValidCommand(), TestContext.Current.CancellationToken);

        Assert.Equal(expectedCode, Assert.Single(result.Error!.ValidationErrors).Code);
        Assert.DoesNotContain("upload", harness.Journal.Calls);
        Assert.False(store.ReplaceWasCalled);
    }

    [Fact]
    public async Task Replace_WhenModerationIsUnavailable_FailsClosedAsDependencyFailure()
    {
        var harness = new MediaHarness(verdict: ImageModerationVerdict.Unavailable);
        var store = new FakeAvatarStore();

        var result = await CreateReplaceHandler(harness, store).HandleAsync(
            ValidCommand(), TestContext.Current.CancellationToken);

        Assert.Equal("portal_avatar_moderation_unavailable", result.Error!.Code);
        Assert.DoesNotContain("upload", harness.Journal.Calls);
        Assert.False(store.ReplaceWasCalled);
    }

    [Fact]
    public async Task Replace_WhenApproved_PersistsTheUploadedPair()
    {
        var harness = new MediaHarness();
        var store = new FakeAvatarStore();

        var result = await CreateReplaceHandler(harness, store).HandleAsync(
            ValidCommand(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(FakeMediaStorage.UploadedUrl, store.PersistedUrl);
        Assert.Equal(FakeMediaStorage.UploadedPublicId, store.PersistedPublicId);
        Assert.DoesNotContain("delete", harness.Journal.Calls);
    }

    [Fact]
    public async Task Replace_WhenProfileIsMissing_CompensatesTheUnreferencedUpload()
    {
        var harness = new MediaHarness();
        var store = new FakeAvatarStore { Outcome = MyAvatarOutcome.NotFound };

        var result = await CreateReplaceHandler(harness, store).HandleAsync(
            ValidCommand(), TestContext.Current.CancellationToken);

        Assert.Equal(ClientPortalErrors.ProfileNotAvailable.Code, result.Error!.Code);
        Assert.Equal(FakeMediaStorage.UploadedPublicId, harness.Storage.DeletedPublicId);
        Assert.Equal(TrainerId, harness.Storage.DeletedForTrainer);
    }

    [Fact]
    public async Task Replace_WhenProfileIsMissingAndCleanupFails_ReportsManualCleanup()
    {
        var harness = new MediaHarness(throwOnDelete: true);
        var store = new FakeAvatarStore { Outcome = MyAvatarOutcome.NotFound };

        var result = await CreateReplaceHandler(harness, store).HandleAsync(
            ValidCommand(), TestContext.Current.CancellationToken);

        Assert.Equal(ClientPortalErrors.AvatarCompensationFailed.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Replace_WhenPersistenceFails_CompensatesAndReportsPersistenceFailure()
    {
        var harness = new MediaHarness();
        var store = new FakeAvatarStore { ThrowOnReplace = true };

        var result = await CreateReplaceHandler(harness, store).HandleAsync(
            ValidCommand(), TestContext.Current.CancellationToken);

        Assert.Equal(ClientPortalErrors.AvatarPersistenceFailed.Code, result.Error!.Code);
        Assert.Equal(FakeMediaStorage.UploadedPublicId, harness.Storage.DeletedPublicId);
        Assert.Equal(CancellationToken.None, harness.Storage.DeleteToken);
    }

    [Fact]
    public async Task Replace_WhenCompensationFails_ReportsManualCleanup()
    {
        var harness = new MediaHarness(throwOnDelete: true);
        var store = new FakeAvatarStore { ThrowOnReplace = true };

        var result = await CreateReplaceHandler(harness, store).HandleAsync(
            ValidCommand(), TestContext.Current.CancellationToken);

        Assert.Equal(ClientPortalErrors.AvatarCompensationFailed.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Replace_WhenPersistenceIsCancelled_CompensatesAndRethrows()
    {
        var harness = new MediaHarness();
        var store = new FakeAvatarStore { CancelOnReplace = true };

        var action = () => CreateReplaceHandler(harness, store).HandleAsync(
            ValidCommand(), TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<OperationCanceledException>(action);
        Assert.Equal(FakeMediaStorage.UploadedPublicId, harness.Storage.DeletedPublicId);
    }

    [Fact]
    public async Task Remove_WhenActorIsTrainer_ReturnsForbidden()
    {
        var store = new FakeAvatarStore();
        var handler = new RemoveMyAvatarHandler(new StubTenantContext("trainer"), new StubClock(), store);

        var result = await handler.HandleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ClientPortalErrors.ClientOnly.Code, result.Error!.Code);
        Assert.False(store.RemoveWasCalled);
    }

    [Fact]
    public async Task Remove_WhenProfileExists_ReturnsUpdatedProfile()
    {
        var store = new FakeAvatarStore();
        var handler = new RemoveMyAvatarHandler(new StubTenantContext("client"), new StubClock(), store);

        var result = await handler.HandleAsync(TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal((TrainerId, ClientUserId), (store.TrainerId, store.ClientUserId));
    }

    [Fact]
    public async Task Remove_WhenProfileIsMissing_ReturnsNotAvailable()
    {
        var store = new FakeAvatarStore { Outcome = MyAvatarOutcome.NotFound };
        var handler = new RemoveMyAvatarHandler(new StubTenantContext("client"), new StubClock(), store);

        var result = await handler.HandleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ClientPortalErrors.ProfileNotAvailable.Code, result.Error!.Code);
    }

    private static ReplaceMyAvatarHandler CreateReplaceHandler(
        MediaHarness harness,
        FakeAvatarStore store,
        string role = "client") =>
        new(new ReplaceMyAvatarCommandValidator(), new StubTenantContext(role), new StubClock(),
            harness.Pipeline, harness.Storage, store);

    private static ReplaceMyAvatarCommand ValidCommand() => new(MediaHarness.ValidUpload());

    private static MyProfileDto Profile() => new(
        "Ana", null, "+351912345678", new DateOnly(1990, 1, 1), "female", null, null,
        FakeMediaStorage.UploadedUrl, Now);

    private sealed class FakeAvatarStore : IMyAvatarStore
    {
        public MyAvatarOutcome Outcome { get; init; } = MyAvatarOutcome.Updated(Profile());
        public bool ThrowOnReplace { get; init; }
        public bool CancelOnReplace { get; init; }
        public bool ReplaceWasCalled { get; private set; }
        public bool RemoveWasCalled { get; private set; }
        public Guid TrainerId { get; private set; }
        public Guid ClientUserId { get; private set; }
        public string? PersistedUrl { get; private set; }
        public string? PersistedPublicId { get; private set; }

        public Task<MyAvatarOutcome> ReplaceAsync(Guid trainerId, Guid clientUserId, string avatarUrl,
            string avatarPublicId, Guid correlationId, DateTime now, CancellationToken cancellationToken)
        {
            ReplaceWasCalled = true;
            (TrainerId, ClientUserId, PersistedUrl, PersistedPublicId) =
                (trainerId, clientUserId, avatarUrl, avatarPublicId);

            if (CancelOnReplace)
                throw new OperationCanceledException(cancellationToken);

            return ThrowOnReplace
                ? throw new InvalidOperationException("Persistence failed.")
                : Task.FromResult(Outcome);
        }

        public Task<MyAvatarOutcome> RemoveAsync(Guid trainerId, Guid clientUserId, Guid correlationId,
            DateTime now, CancellationToken cancellationToken)
        {
            RemoveWasCalled = true;
            (TrainerId, ClientUserId) = (trainerId, clientUserId);
            return Task.FromResult(Outcome);
        }
    }

    private sealed class StubTenantContext(string role) : ITenantContext
    {
        public Guid? TrainerId => MyAvatarHandlerTests.TrainerId;
        public Guid? UserId => ClientUserId;
        public string? Role => role;
        public TenantOrigin Origin => TenantOrigin.Http;
        public bool IsAdministrative => false;
    }

    private sealed class StubClock : IClock
    {
        public DateTime UtcNow => Now;
    }
}
