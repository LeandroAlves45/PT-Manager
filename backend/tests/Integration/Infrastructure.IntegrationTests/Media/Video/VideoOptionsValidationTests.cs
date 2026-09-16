using Infrastructure.Media.Video;

namespace Infrastructure.IntegrationTests.Media.Video;

/// <summary>
/// Prova os kill-switches e os limites configuráveis: desligado, o R2 não exige
/// segredos; ligado sem configuração segura, ou com limites fora do intervalo, o
/// arranque falha.
/// </summary>
public sealed class VideoOptionsValidationTests
{
    private const string ValidAccountId = "0123456789abcdef0123456789abcdef";

    [Fact]
    public void R2_WhenDisabled_DoesNotRequireSecrets()
    {
        Assert.True(new R2OptionsValidator().Validate(null, new R2Options()).Succeeded);
    }

    [Fact]
    public void R2_WhenEnabledWithoutConfiguration_ReportsEveryMissingValue()
    {
        var result = new R2OptionsValidator().Validate(null, new R2Options { Enabled = true });

        Assert.True(result.Failed);
        Assert.Equal(4, result.Failures!.Count());
    }

    [Theory]
    [InlineData("ABCDEF0123456789ABCDEF0123456789")]
    [InlineData("0123456789abcdef")]
    [InlineData("0123456789abcdef0123456789abcdeg")]
    public void R2_RejectsMalformedAccountIds(string accountId)
    {
        Assert.True(new R2OptionsValidator().Validate(null, Valid(accountId: accountId)).Failed);
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("prefix/")]
    [InlineData("Upper")]
    [InlineData("")]
    public void R2_RejectsUnsafeKeyPrefixes(string prefix)
    {
        Assert.True(new R2OptionsValidator().Validate(null, Valid(keyPrefix: prefix)).Failed);
    }

    [Fact]
    public void R2_RejectsNonHttpsServiceUrlsAndLongTimeouts()
    {
        Assert.True(new R2OptionsValidator().Validate(
            null, Valid(serviceUrl: new Uri("http://s3.test/"))).Failed);
        Assert.True(new R2OptionsValidator().Validate(
            null, Valid(timeout: TimeSpan.FromMinutes(2))).Failed);
    }

    [Fact]
    public void R2_AcceptsACompleteConfiguration()
    {
        Assert.True(new R2OptionsValidator().Validate(null, Valid()).Succeeded);
    }

    [Fact]
    public void ExerciseVideos_DefaultsAreTheApprovedDecisions()
    {
        var settings = new ExerciseVideoOptions().ToSettings();

        Assert.Equal(
            (20, TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(30), TimeSpan.FromHours(1)),
            (settings.MaxVideosPerTrainer, settings.UploadUrlLifetime, settings.PlaybackUrlLifetime, settings.AbandonmentGrace));
    }

    [Theory]
    [InlineData(0, 15, 30, 60)]
    [InlineData(20, 0, 30, 60)]
    [InlineData(20, 120, 30, 60)]
    [InlineData(20, 15, 0, 60)]
    [InlineData(20, 15, 30, 0)]
    [InlineData(20, 15, 30, 30)]
    public void ExerciseVideos_RejectsValuesOutsideTheAllowedRanges(
        int maxVideos, int uploadMinutes, int playbackMinutes, int graceMinutes)
    {
        var result = new ExerciseVideoOptionsValidator().Validate(null, new ExerciseVideoOptions
        {
            MaxVideosPerTrainer = maxVideos,
            UploadUrlLifetime = TimeSpan.FromMinutes(uploadMinutes),
            PlaybackUrlLifetime = TimeSpan.FromMinutes(playbackMinutes),
            AbandonmentGrace = TimeSpan.FromMinutes(graceMinutes)
        });

        Assert.True(result.Failed);
    }

    private static R2Options Valid(
        string accountId = ValidAccountId,
        string keyPrefix = "pt-manager",
        Uri? serviceUrl = null,
        TimeSpan? timeout = null) => new()
        {
            Enabled = true,
            AccountId = accountId,
            AccessKeyId = "access",
            SecretAccessKey = "secret",
            BucketName = "pt-manager-videos",
            KeyPrefix = keyPrefix,
            ServiceUrl = serviceUrl,
            Timeout = timeout ?? TimeSpan.FromSeconds(20)
        };
}
