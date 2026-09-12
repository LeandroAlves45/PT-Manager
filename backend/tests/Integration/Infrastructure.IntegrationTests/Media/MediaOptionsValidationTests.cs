using Infrastructure.Media.Cloudinary;
using Infrastructure.Media.Moderation;

namespace Infrastructure.IntegrationTests.Media;

/// <summary>
/// Prova o contrato dos kill-switches: desligados, os fornecedores não exigem
/// segredos; ligados sem configuração segura, o arranque falha.
/// </summary>
public sealed class MediaOptionsValidationTests
{
    [Fact]
    public void Cloudinary_WhenDisabled_DoesNotRequireSecrets()
    {
        var result = new CloudinaryOptionsValidator().Validate(null, new CloudinaryOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Cloudinary_WhenEnabledWithoutSecrets_Fails()
    {
        var result = new CloudinaryOptionsValidator().Validate(null, new CloudinaryOptions { Enabled = true });

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, failure => failure.Contains("API secret", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("root/")]
    [InlineData("Root With Spaces")]
    [InlineData("")]
    public void Cloudinary_RejectsUnsafeFolderRoots(string folderRoot)
    {
        var result = new CloudinaryOptionsValidator().Validate(
            null, new CloudinaryOptions { FolderRoot = folderRoot });

        Assert.True(result.Failed);
    }

    [Fact]
    public void Cloudinary_RejectsNonHttpsBaseAddress()
    {
        var result = new CloudinaryOptionsValidator().Validate(
            null, new CloudinaryOptions { BaseAddress = new Uri("http://api.cloudinary.com/") });

        Assert.True(result.Failed);
    }

    [Fact]
    public void Vision_WhenDisabled_DoesNotRequireCredentials()
    {
        Assert.True(new VisionOptionsValidator().Validate(null, new VisionOptions()).Succeeded);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not json")]
    [InlineData("""{"type":"authorized_user","client_email":"a","private_key":"b"}""")]
    [InlineData("""{"type":"service_account","client_email":"a"}""")]
    public void Vision_WhenEnabled_RequiresAServiceAccountShape(string? json)
    {
        var result = new VisionOptionsValidator().Validate(
            null, new VisionOptions { Enabled = true, ServiceAccountJson = json });

        Assert.True(result.Failed);
        Assert.DoesNotContain(result.Failures!, failure => json is not null && failure.Contains(json, StringComparison.Ordinal));
    }

    [Fact]
    public void Vision_RejectsTimeoutsThatWouldHoldTheRequestHostage()
    {
        var result = new VisionOptionsValidator().Validate(
            null, new VisionOptions { Timeout = TimeSpan.FromMinutes(2) });

        Assert.True(result.Failed);
    }
}
