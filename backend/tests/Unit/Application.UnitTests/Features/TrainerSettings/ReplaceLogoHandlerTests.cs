using Application.Common.Abstractions;
using Application.Features.TrainerSettings;
using Application.Features.TrainerSettings.Abstractions;
using Application.Features.TrainerSettings.ReplaceLogo;
using Application.UnitTests.Common.Media;
using TrainerSettingsEntity = Domain.Entities.TrainerSettings.TrainerSettings;

namespace Application.UnitTests.Features.TrainerSettings;

/// <summary>
/// Prova o ponto 4 do Gate 5C para o logo: falhas preservam o asset anterior e
/// compensam o novo quando necessário.
/// </summary>
public sealed class ReplaceLogoHandlerTests
{
    private static readonly DateTime Now = new(2026, 8, 20, 10, 0, 0, DateTimeKind.Utc);
    private static readonly Guid TrainerId = Guid.NewGuid();

    [Fact]
    public async Task HandleAsync_WhenLogoIsNull_ReturnsValidationWithoutExternalIo()
    {
        var harness = new MediaHarness();
        var store = new FakeStore();

        var result = await CreateHandler(harness, store).HandleAsync(
            new ReplaceLogoCommand(null!), TestContext.Current.CancellationToken);

        Assert.Equal("validation_failed", result.Error!.Code);
        Assert.Empty(harness.Journal.Calls);
        Assert.False(store.WasCalled);
    }

    [Fact]
    public async Task HandleAsync_WhenActorIsNotTrainer_ReturnsForbiddenWithoutExternalIo()
    {
        var harness = new MediaHarness();
        var store = new FakeStore();
        var handler = new ReplaceLogoHandler(
            new ReplaceLogoCommandValidator(), new StubTenantContext("client"), new StubClock(),
            harness.Pipeline, harness.Storage, store);

        var result = await handler.HandleAsync(ValidCommand(), TestContext.Current.CancellationToken);

        Assert.Equal(TrainerSettingsErrors.TrainerOnly.Code, result.Error!.Code);
        Assert.Empty(harness.Journal.Calls);
    }

    [Fact]
    public async Task HandleAsync_WhenImageIsInvalid_ReturnsSpecificValidationWithoutPersistence()
    {
        var harness = new MediaHarness(processingFailure: ImageValidationFailure.NotDecodable);
        var store = new FakeStore();

        var result = await CreateHandler(harness, store).HandleAsync(
            ValidCommand(), TestContext.Current.CancellationToken);

        Assert.Equal("trainer_settings_logo_not_decodable", Assert.Single(result.Error!.ValidationErrors).Code);
        Assert.False(store.WasCalled);
    }

    [Fact]
    public async Task HandleAsync_WhenStorageIsDisabled_ReturnsDependencyFailureWithoutPersistence()
    {
        var harness = new MediaHarness(uploadStatus: MediaStorageStatus.Disabled);
        var store = new FakeStore();

        var result = await CreateHandler(harness, store).HandleAsync(
            ValidCommand(), TestContext.Current.CancellationToken);

        Assert.Equal("trainer_settings_logo_storage_unavailable", result.Error!.Code);
        Assert.False(store.WasCalled);
    }

    [Fact]
    public async Task HandleAsync_DoesNotModerateTheLogo()
    {
        var harness = new MediaHarness(verdict: ImageModerationVerdict.Unavailable);
        var store = new FakeStore();

        var result = await CreateHandler(harness, store).HandleAsync(
            ValidCommand(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain("moderate", harness.Journal.Calls);
    }

    [Fact]
    public async Task HandleAsync_WhenPersistenceSucceeds_ReturnsNewLogoWithoutCompensation()
    {
        var settings = new TrainerSettingsEntity(TrainerId, Now);
        settings.ReplaceLogo(FakeMediaStorage.UploadedUrl, FakeMediaStorage.UploadedPublicId, Now);
        var harness = new MediaHarness();
        var store = new FakeStore { Result = TrainerSettingsStoreResult.Updated(settings) };

        var result = await CreateHandler(harness, store).HandleAsync(
            ValidCommand(), TestContext.Current.CancellationToken);

        Assert.Equal(FakeMediaStorage.UploadedUrl, result.Value.LogoUrl);
        Assert.Equal(FakeMediaStorage.UploadedPublicId, store.PersistedPublicId);
        Assert.DoesNotContain("delete", harness.Journal.Calls);
    }

    [Fact]
    public async Task HandleAsync_WhenPersistenceFailsAndCompensationSucceeds_DeletesOnlyTheNewAsset()
    {
        var harness = new MediaHarness();
        var store = new FakeStore { ThrowOnReplace = true };

        var result = await CreateHandler(harness, store).HandleAsync(
            ValidCommand(), TestContext.Current.CancellationToken);

        Assert.Equal(TrainerSettingsErrors.PersistenceFailed.Code, result.Error!.Code);
        Assert.Equal(FakeMediaStorage.UploadedPublicId, harness.Storage.DeletedPublicId);
        Assert.Equal(TrainerId, harness.Storage.DeletedForTrainer);
        Assert.Equal(CancellationToken.None, harness.Storage.DeleteToken);
    }

    [Fact]
    public async Task HandleAsync_WhenCompensationThrows_ReturnsManualCleanupFailure()
    {
        var harness = new MediaHarness(throwOnDelete: true);
        var store = new FakeStore { ThrowOnReplace = true };

        var result = await CreateHandler(harness, store).HandleAsync(
            ValidCommand(), TestContext.Current.CancellationToken);

        Assert.Equal(TrainerSettingsErrors.LogoCompensationFailed.Code, result.Error!.Code);
    }

    [Fact]
    public async Task HandleAsync_WhenCompensationIsNotConfirmed_ReturnsManualCleanupFailure()
    {
        var harness = new MediaHarness(deleteStatus: MediaStorageStatus.TransientFailure);
        var store = new FakeStore { ThrowOnReplace = true };

        var result = await CreateHandler(harness, store).HandleAsync(
            ValidCommand(), TestContext.Current.CancellationToken);

        Assert.Equal(TrainerSettingsErrors.LogoCompensationFailed.Code, result.Error!.Code);
    }

    [Fact]
    public async Task HandleAsync_WhenPersistenceIsCancelled_CompensatesAndRethrowsCancellation()
    {
        var harness = new MediaHarness();
        var store = new FakeStore { CancelOnReplace = true };

        var action = () => CreateHandler(harness, store).HandleAsync(
            ValidCommand(), TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<OperationCanceledException>(action);
        Assert.Equal(FakeMediaStorage.UploadedPublicId, harness.Storage.DeletedPublicId);
    }

    [Fact]
    public async Task HandleAsync_WhenCancellationCompensationFails_ReturnsManualCleanupFailure()
    {
        var harness = new MediaHarness(throwOnDelete: true);
        var store = new FakeStore { CancelOnReplace = true };

        var result = await CreateHandler(harness, store).HandleAsync(
            ValidCommand(), TestContext.Current.CancellationToken);

        Assert.Equal(TrainerSettingsErrors.LogoCompensationFailed.Code, result.Error!.Code);
    }

    private static ReplaceLogoHandler CreateHandler(MediaHarness harness, FakeStore store) =>
        new(new ReplaceLogoCommandValidator(), new StubTenantContext("trainer"), new StubClock(),
            harness.Pipeline, harness.Storage, store);

    private static ReplaceLogoCommand ValidCommand() => new(MediaHarness.ValidUpload());

    private sealed class FakeStore : ITrainerSettingsStore
    {
        public bool ThrowOnReplace { get; init; }
        public bool CancelOnReplace { get; init; }
        public bool WasCalled { get; private set; }
        public string? PersistedPublicId { get; private set; }
        public TrainerSettingsStoreResult Result { get; init; } = TrainerSettingsStoreResult.Updated(
            new TrainerSettingsEntity(TrainerId, Now));

        public Task<TrainerSettingsStoreResult> ReplaceLogoAsync(Guid trainerId, string logoUrl,
            string logoPublicId, Guid correlationId, DateTime now,
            CancellationToken cancellationToken)
        {
            WasCalled = true;
            PersistedPublicId = logoPublicId;
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

    private sealed class StubTenantContext(string role) : ITenantContext
    {
        public Guid? TrainerId => ReplaceLogoHandlerTests.TrainerId;
        public Guid? UserId => ReplaceLogoHandlerTests.TrainerId;
        public string? Role => role;
        public TenantOrigin Origin => TenantOrigin.Http;
        public bool IsAdministrative => false;
    }

    private sealed class StubClock : IClock
    {
        public DateTime UtcNow => Now;
    }
}
