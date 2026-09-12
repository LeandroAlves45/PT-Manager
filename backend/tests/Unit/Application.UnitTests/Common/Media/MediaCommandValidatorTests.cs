using Application.Common.Abstractions;
using Application.Features.ClientPortal.ReplaceMyAvatar;
using Application.Features.TrainerSettings.ReplaceLogo;

namespace Application.UnitTests.Common.Media;

/// <summary>
/// Prova as regras baratas da fronteira: presença, Content-Type declarado e
/// tamanho declarado, com limites lidos da mesma fonte que o processador.
/// </summary>
public sealed class MediaCommandValidatorTests
{
    [Theory]
    [InlineData("image/gif", "trainer_settings_unsupported_media_type")]
    [InlineData("image/svg+xml", "trainer_settings_unsupported_media_type")]
    [InlineData("application/pdf", "trainer_settings_unsupported_media_type")]
    public void Logo_RejectsContentTypesOutsideTheAllowlist(string contentType, string expectedCode)
    {
        var result = new ReplaceLogoCommandValidator().Validate(
            new ReplaceLogoCommand(Upload(contentType, 100)));

        Assert.Contains(result.Errors, error => error.ErrorCode == expectedCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5L * 1024 * 1024 + 1)]
    public void Logo_RejectsEmptyAndOversizedDeclarations(long length)
    {
        var result = new ReplaceLogoCommandValidator().Validate(
            new ReplaceLogoCommand(Upload("image/png", length)));

        Assert.Contains(result.Errors, error => error.ErrorCode == "trainer_settings_media_too_large");
    }

    [Fact]
    public void Logo_AcceptsTheProfileMaximum()
    {
        var result = new ReplaceLogoCommandValidator().Validate(
            new ReplaceLogoCommand(Upload("IMAGE/PNG", ImageProfiles.TrainerLogo.MaxBytes)));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Avatar_UsesTheSmallerAvatarLimit()
    {
        var result = new ReplaceMyAvatarCommandValidator().Validate(
            new ReplaceMyAvatarCommand(Upload("image/jpeg", ImageProfiles.ClientAvatar.MaxBytes + 1)));

        Assert.Contains(result.Errors, error => error.ErrorCode == "portal_avatar_media_too_large");
    }

    [Fact]
    public void Avatar_RequiresTheFile()
    {
        var result = new ReplaceMyAvatarCommandValidator().Validate(new ReplaceMyAvatarCommand(null!));

        Assert.Contains(result.Errors, error => error.ErrorCode == "portal_avatar_required");
    }

    private static MediaUpload Upload(string contentType, long length) =>
        new(Stream.Null, contentType, length);
}
