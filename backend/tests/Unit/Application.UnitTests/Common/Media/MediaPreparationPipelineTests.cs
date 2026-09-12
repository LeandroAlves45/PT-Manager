using Application.Common.Abstractions;
using Application.Common.Media;
using Application.Errors;

namespace Application.UnitTests.Common.Media;

/// <summary>
/// Prova a política do pipeline: a ordem das etapas, a moderação por perfil e o
/// comportamento fail-closed (ponto 3 do Gate 5C).
/// </summary>
public sealed class MediaPreparationPipelineTests
{
    private static readonly Guid TrainerId = Guid.NewGuid();

    [Fact]
    public async Task PrepareAsync_WhenAvatarIsApproved_RunsNormalizeModerateUploadInThatOrder()
    {
        var harness = new MediaHarness();

        var outcome = await PrepareAsync(harness, ImageProfiles.ClientAvatar, MediaAssetKind.ClientAvatar);

        Assert.Equal(MediaPreparationStatus.Prepared, outcome.Status);
        Assert.Equal(["normalize", "moderate", "upload"], harness.Journal.Calls);
    }

    [Fact]
    public async Task PrepareAsync_ModeratesTheSameBytesThatAreUploaded()
    {
        var harness = new MediaHarness();

        await PrepareAsync(harness, ImageProfiles.ClientAvatar, MediaAssetKind.ClientAvatar);

        Assert.Equal(
            harness.Moderation.ReviewedContent!.Value.ToArray(),
            harness.Storage.UploadedRequest!.Content.ToArray());
    }

    [Fact]
    public async Task PrepareAsync_WhenLogoIsPrepared_SkipsModeration()
    {
        var harness = new MediaHarness(verdict: ImageModerationVerdict.Rejected);

        var outcome = await PrepareAsync(harness, ImageProfiles.TrainerLogo, MediaAssetKind.TrainerLogo);

        Assert.Equal(MediaPreparationStatus.Prepared, outcome.Status);
        Assert.Equal(["normalize", "upload"], harness.Journal.Calls);
    }

    [Theory]
    [InlineData(ImageModerationVerdict.Rejected, MediaPreparationStatus.Rejected)]
    [InlineData(ImageModerationVerdict.ReviewRequired, MediaPreparationStatus.ReviewRequired)]
    [InlineData(ImageModerationVerdict.Unavailable, MediaPreparationStatus.ModerationUnavailable)]
    [InlineData((ImageModerationVerdict)99, MediaPreparationStatus.ModerationUnavailable)]
    public async Task PrepareAsync_WhenAvatarIsNotApproved_NeverUploads(
        ImageModerationVerdict verdict,
        MediaPreparationStatus expected)
    {
        var harness = new MediaHarness(verdict: verdict);

        var outcome = await PrepareAsync(harness, ImageProfiles.ClientAvatar, MediaAssetKind.ClientAvatar);

        Assert.Equal(expected, outcome.Status);
        Assert.Null(outcome.Media);
        Assert.DoesNotContain("upload", harness.Journal.Calls);
    }

    [Fact]
    public async Task PrepareAsync_WhenImageIsInvalid_StopsBeforeModerationAndUpload()
    {
        var harness = new MediaHarness(processingFailure: ImageValidationFailure.ContentTypeMismatch);

        var outcome = await PrepareAsync(harness, ImageProfiles.ClientAvatar, MediaAssetKind.ClientAvatar);

        Assert.Equal(MediaPreparationStatus.InvalidImage, outcome.Status);
        Assert.Equal(ImageValidationFailure.ContentTypeMismatch, outcome.ValidationFailure);
        Assert.Equal(["normalize"], harness.Journal.Calls);
    }

    [Theory]
    [InlineData(MediaStorageStatus.Disabled, MediaPreparationStatus.StorageDisabled)]
    [InlineData(MediaStorageStatus.TransientFailure, MediaPreparationStatus.StorageFailed)]
    [InlineData(MediaStorageStatus.PermanentFailure, MediaPreparationStatus.StorageFailed)]
    [InlineData(MediaStorageStatus.InvalidResponse, MediaPreparationStatus.StorageFailed)]
    public async Task PrepareAsync_WhenStorageFails_ReportsWithoutMedia(
        MediaStorageStatus storageStatus,
        MediaPreparationStatus expected)
    {
        var harness = new MediaHarness(uploadStatus: storageStatus);

        var outcome = await PrepareAsync(harness, ImageProfiles.TrainerLogo, MediaAssetKind.TrainerLogo);

        Assert.Equal(expected, outcome.Status);
        Assert.Null(outcome.Media);
    }

    [Fact]
    public async Task PrepareAsync_PassesTenantAndKindToStorage()
    {
        var harness = new MediaHarness();

        await PrepareAsync(harness, ImageProfiles.ClientAvatar, MediaAssetKind.ClientAvatar);

        Assert.Equal(TrainerId, harness.Storage.UploadedRequest!.TrainerId);
        Assert.Equal(MediaAssetKind.ClientAvatar, harness.Storage.UploadedRequest.Kind);
        Assert.Equal("image/webp", harness.Storage.UploadedRequest.ContentType);
    }

    /// <summary>
    /// Congela a política de moderação por perfil. Inverter qualquer um destes
    /// valores é uma decisão de produto e de segurança, não uma refactorização.
    /// </summary>
    [Fact]
    public void ImageProfiles_ModerationPolicyIsFrozen()
    {
        Assert.True(ImageProfiles.ClientAvatar.RequiresModeration);
        Assert.False(ImageProfiles.TrainerLogo.RequiresModeration);
    }

    [Theory]
    [InlineData(MediaPreparationStatus.Rejected)]
    [InlineData(MediaPreparationStatus.ReviewRequired)]
    public void ErrorMapper_RejectionAndReviewAreIndistinguishableToTheClient(MediaPreparationStatus status)
    {
        var error = MediaPreparationErrorMapper.ToError(
            MediaPreparationOutcome.From(status), "Avatar", "portal_avatar");

        Assert.Equal(ErrorCategory.Validation, error.Category);
        Assert.Equal("portal_avatar_content_not_allowed", Assert.Single(error.ValidationErrors).Code);
    }

    [Theory]
    [InlineData(MediaPreparationStatus.ModerationUnavailable, "portal_avatar_moderation_unavailable")]
    [InlineData(MediaPreparationStatus.StorageDisabled, "portal_avatar_storage_unavailable")]
    [InlineData(MediaPreparationStatus.StorageFailed, "portal_avatar_storage_unavailable")]
    public void ErrorMapper_DependencyFailuresMapToExternalDependency(
        MediaPreparationStatus status,
        string expectedCode)
    {
        var error = MediaPreparationErrorMapper.ToError(
            MediaPreparationOutcome.From(status), "Avatar", "portal_avatar");

        Assert.Equal(ErrorCategory.ExternalDependency, error.Category);
        Assert.Equal(expectedCode, error.Code);
    }

    [Fact]
    public void ErrorMapper_InvalidImageCarriesTheSpecificFailureCode()
    {
        var error = MediaPreparationErrorMapper.ToError(
            MediaPreparationOutcome.InvalidImage(ImageValidationFailure.PixelBudgetExceeded),
            "Logo",
            "trainer_settings_logo");

        var validation = Assert.Single(error.ValidationErrors);
        Assert.Equal("Logo", validation.Field);
        Assert.Equal("trainer_settings_logo_pixel_budget_exceeded", validation.Code);
    }

    private static Task<MediaPreparationOutcome> PrepareAsync(
        MediaHarness harness,
        ImageProfile profile,
        MediaAssetKind kind) =>
        harness.Pipeline.PrepareAsync(
            MediaHarness.ValidUpload(), profile, kind, TrainerId, TestContext.Current.CancellationToken);
}
