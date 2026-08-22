using Application.Common.Abstractions;
using Application.Features.TrainerSettings;
using Application.Features.TrainerSettings.Abstractions;
using Application.Features.TrainerSettings.ReplaceLogo;
using TrainerSettingsEntity = Domain.Entities.TrainerSettings.TrainerSettings;

namespace Application.UnitTests.Features.TrainerSettings;

public sealed class ReplaceLogoHandlerTests
{
    private static readonly DateTime Now = new(2026, 8, 20, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task HandleAsync_WhenLogoIsNull_ReturnsValidationWithoutExternalIo()
    {
        var store = new FakeStore();
        var media = new FakeMediaStorage();
        var handler = CreateHandler(store, media);

        var result = await handler.HandleAsync(
            new ReplaceLogoCommand(null!), TestContext.Current.CancellationToken);

        Assert.Equal(("validation_failed", false, false),
            (result.Error!.Code, media.UploadWasCalled, store.WasCalled));
    }

    [Fact]
    public async Task HandleAsync_WhenUploadFails_ReturnsMediaFailureWithoutPersistence()
    {
        var store = new FakeStore();
        var media = new FakeMediaStorage { ThrowOnUpload = true };
        var handler = CreateHandler(store, media);

        var result = await handler.HandleAsync(ValidCommand(), TestContext.Current.CancellationToken);

        Assert.Equal((TrainerSettingsErrors.MediaUploadFailed.Code, false),
            (result.Error!.Code, store.WasCalled));
    }

    [Fact]
    public async Task HandleAsync_WhenPersistenceSucceeds_ReturnsNewLogoWithoutCompensation()
    {
        var settings = new TrainerSettingsEntity(Guid.NewGuid(), Now);
        settings.ReplaceLogo("https://cdn/new-logo.png", "new-public-id", Now);
        var store = new FakeStore { Result = TrainerSettingsStoreResult.Updated(settings) };
        var media = new FakeMediaStorage();
        var handler = CreateHandler(store, media);

        var result = await handler.HandleAsync(ValidCommand(), TestContext.Current.CancellationToken);

        Assert.Equal(("https://cdn/new-logo.png", false),
            (result.Value.LogoUrl, media.DeleteWasCalled));
    }

    [Fact]
    public async Task HandleAsync_WhenPersistenceFailsAndCompensationSucceeds_ReturnsPersistenceFailure()
    {
        var store = new FakeStore { ThrowOnReplace = true };
        var media = new FakeMediaStorage();
        var handler = CreateHandler(store, media);

        var result = await handler.HandleAsync(ValidCommand(), TestContext.Current.CancellationToken);

        Assert.Equal((TrainerSettingsErrors.PersistenceFailed.Code, "new-public-id"),
            (result.Error!.Code, media.DeletedPublicId));
    }

    [Fact]
    public async Task HandleAsync_WhenCompensationFails_ReturnsManualCleanupFailure()
    {
        var store = new FakeStore { ThrowOnReplace = true };
        var media = new FakeMediaStorage { ThrowOnDelete = true };
        var handler = CreateHandler(store, media);

        var result = await handler.HandleAsync(ValidCommand(), TestContext.Current.CancellationToken);

        Assert.Equal(TrainerSettingsErrors.LogoCompensationFailed.Code, result.Error!.Code);
    }

    [Fact]
    public async Task HandleAsync_WhenPersistenceIsCancelled_CompensatesAndRethrowsCancellation()
    {
        var store = new FakeStore { CancelOnReplace = true };
        var media = new FakeMediaStorage();
        var handler = CreateHandler(store, media);

        var action = () => handler.HandleAsync(
            ValidCommand(), TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<OperationCanceledException>(action);
        Assert.Equal("new-public-id", media.DeletedPublicId);
    }

    [Fact]
    public async Task HandleAsync_WhenCancellationCompensationFails_ReturnsManualCleanupFailure()
    {
        var store = new FakeStore { CancelOnReplace = true };
        var media = new FakeMediaStorage { ThrowOnDelete = true };
        var handler = CreateHandler(store, media);

        var result = await handler.HandleAsync(
            ValidCommand(), TestContext.Current.CancellationToken);

        Assert.Equal(TrainerSettingsErrors.LogoCompensationFailed.Code, result.Error!.Code);
    }

    private static ReplaceLogoHandler CreateHandler(FakeStore store, FakeMediaStorage media) =>
        new(new ReplaceLogoCommandValidator(), new StubTenantContext(), new StubClock(), media, store);

    private static ReplaceLogoCommand ValidCommand() =>
        new(new MediaUpload(new MemoryStream([1, 2, 3]), "image/png", 3));

    private sealed class FakeMediaStorage : IMediaStorage
    {
        public bool ThrowOnUpload { get; init; }
        public bool ThrowOnDelete { get; init; }
        public bool UploadWasCalled { get; private set; }
        public bool DeleteWasCalled { get; private set; }
        public string? DeletedPublicId { get; private set; }

        public Task<StoredMedia> UploadAsync(MediaUpload upload, CancellationToken cancellationToken)
        {
            UploadWasCalled = true;
            return ThrowOnUpload
                ? throw new InvalidOperationException("Upload failed.")
                : Task.FromResult(new StoredMedia("https://cdn/new-logo.png", "new-public-id"));
        }

        public Task DeleteAsync(string publicId, CancellationToken cancellationToken)
        {
            DeleteWasCalled = true;
            DeletedPublicId = publicId;
            return ThrowOnDelete
                ? throw new InvalidOperationException("Delete failed.")
                : Task.CompletedTask;
        }
    }

    private sealed class FakeStore : ITrainerSettingsStore
    {
        public bool ThrowOnReplace { get; init; }
        public bool CancelOnReplace { get; init; }
        public bool WasCalled { get; private set; }
        public TrainerSettingsStoreResult Result { get; init; } = TrainerSettingsStoreResult.Updated(
            new TrainerSettingsEntity(Guid.NewGuid(), Now));

        public Task<TrainerSettingsStoreResult> ReplaceLogoAsync(Guid trainerId, string logoUrl,
            string logoPublicId, Guid correlationId, DateTime now,
            CancellationToken cancellationToken)
        {
            WasCalled = true;
            if (CancelOnReplace)
                throw new OperationCanceledException(cancellationToken);

            return ThrowOnReplace
                ? throw new InvalidOperationException("Persistence failed.")
                : Task.FromResult(Result);
        }

        public Task<TrainerSettingsStoreResult> UpdateBrandingAsync(Guid trainerId, string appName,
            string? primaryColor, string? bodyColor, DateTime now, CancellationToken cancellationToken) =>
            Task.FromResult(Result);
        public Task<TrainerSettingsStoreResult> ResetBrandingColorsAsync(Guid trainerId,
            DateTime now, CancellationToken cancellationToken) => Task.FromResult(Result);
        public Task<TrainerSettingsStoreResult> UpdateContactsAsync(Guid trainerId, string? phone,
            string? address, string? city, DateTime now, CancellationToken cancellationToken) =>
            Task.FromResult(Result);
        public Task<TrainerSettingsStoreResult> ChangeTimezoneAsync(Guid trainerId, string timezone,
            DateTime now, CancellationToken cancellationToken) => Task.FromResult(Result);
        public Task<TrainerSettingsStoreResult> RemoveLogoAsync(Guid trainerId, Guid correlationId,
            DateTime now, CancellationToken cancellationToken) => Task.FromResult(Result);
    }

    private sealed class StubTenantContext : ITenantContext
    {
        public Guid? TrainerId => Guid.NewGuid();
        public Guid? UserId => Guid.NewGuid();
        public string? Role => "trainer";
        public TenantOrigin Origin => TenantOrigin.Http;
        public bool IsAdministrative => false;
    }

    private sealed class StubClock : IClock
    {
        public DateTime UtcNow => Now;
    }
}
