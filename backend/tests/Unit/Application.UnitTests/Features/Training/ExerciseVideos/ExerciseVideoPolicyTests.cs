using Application.Features.Training.ExerciseVideos;
using Application.Features.Training.ExerciseVideos.Abstractions;

namespace Application.UnitTests.Features.Training.ExerciseVideos;

/// <summary>
/// Congela os limites aprovados e prova que container, codec, duração e
/// resolução são avaliados em conjunto.
/// </summary>
public sealed class ExerciseVideoPolicyTests
{
    [Fact]
    public void Limits_AreTheApprovedProductDecisions()
    {
        Assert.Equal(104_857_600L, ExerciseVideoPolicy.MaxSizeBytes);
        Assert.Equal(TimeSpan.FromMinutes(3), ExerciseVideoPolicy.MaxDuration);
        Assert.Equal(1920, ExerciseVideoPolicy.MaxLongSidePixels);
        Assert.Equal(240, ExerciseVideoPolicy.MinShortSidePixels);
        Assert.Equal(["video/mp4", "video/quicktime"], ExerciseVideoPolicy.AcceptedContentTypes.Order());
        Assert.Equal(["avc1", "avc3"], ExerciseVideoPolicy.AcceptedVideoCodecs.Order());
        Assert.Equal(["mp4a"], ExerciseVideoPolicy.AcceptedAudioCodecs);
    }

    [Fact]
    public void Settings_AcceptTheInclusiveMinimumWindows()
    {
        var settings = new ExerciseVideoSettings(
            1, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1), TimeSpan.FromHours(1));

        Assert.Equal(TimeSpan.FromHours(1), settings.AbandonmentGrace);
    }

    [Fact]
    public void Evaluate_RejectsAudioTracksThatUseAVideoCodec()
    {
        var metadata = new VideoTechnicalMetadata(
            VideoContainer.Mp4, 10_000, 1280, 720, "avc1", "avc1", 1, 1);

        Assert.Equal(
            "exercise_video_audio_codec_unsupported",
            ExerciseVideoPolicy.Evaluate("video/mp4", metadata));
    }

    [Fact]
    public void Evaluate_AcceptsAPortraitPhoneVideoWithAac()
    {
        Assert.Null(ExerciseVideoPolicy.Evaluate("video/mp4", Metadata(width: 1080, height: 1920)));
    }

    [Fact]
    public void Evaluate_AcceptsAQuickTimeVideoWithoutAudio()
    {
        Assert.Null(ExerciseVideoPolicy.Evaluate(
            "video/quicktime",
            Metadata(container: VideoContainer.QuickTime, audioCodec: null, audioTracks: 0)));
    }

    [Fact]
    public void Evaluate_AcceptsExactlyTheMaximumDuration()
    {
        Assert.Null(ExerciseVideoPolicy.Evaluate("video/mp4", Metadata(durationMilliseconds: 180_000)));
    }

    public static TheoryData<string, VideoTechnicalMetadata, string> Rejections() => new()
    {
        { "video/mp4", Metadata(container: VideoContainer.QuickTime), "exercise_video_container_mismatch" },
        { "video/quicktime", Metadata(), "exercise_video_container_mismatch" },
        { "video/webm", Metadata(), "exercise_video_container_mismatch" },
        { "video/mp4", Metadata(videoTracks: 2), "exercise_video_track_layout_unsupported" },
        { "video/mp4", Metadata(videoTracks: 0), "exercise_video_track_layout_unsupported" },
        { "video/mp4", Metadata(audioTracks: 2), "exercise_video_track_layout_unsupported" },
        { "video/mp4", Metadata(videoCodec: "hvc1"), "exercise_video_codec_unsupported" },
        { "video/mp4", Metadata(videoCodec: "????"), "exercise_video_codec_unsupported" },
        { "video/mp4", Metadata(audioCodec: "Opus"), "exercise_video_audio_codec_unsupported" },
        { "video/mp4", Metadata(audioCodec: null), "exercise_video_audio_codec_unsupported" },
        { "video/mp4", Metadata(durationMilliseconds: 0), "exercise_video_duration_invalid" },
        { "video/mp4", Metadata(durationMilliseconds: 180_001), "exercise_video_duration_exceeded" },
        { "video/mp4", Metadata(width: 2560, height: 1440), "exercise_video_resolution_exceeded" },
        { "video/mp4", Metadata(width: 1080, height: 2400), "exercise_video_resolution_exceeded" },
        { "video/mp4", Metadata(width: 320, height: 180), "exercise_video_resolution_too_small" }
    };

    [Theory]
    [InlineData("video/mp4", "video/mp4")]
    [InlineData("video/mp4", "VIDEO/MP4")]
    [InlineData("video/mp4", "video/mp4; charset=utf-8")]
    [InlineData("video/quicktime", "video/quicktime; codecs=avc1")]
    public void ContentTypesMatch_IgnoresParametersAndCasing(string declared, string stored)
    {
        Assert.True(ExerciseVideoPolicy.ContentTypesMatch(declared, stored));
    }

    [Theory]
    [InlineData("video/mp4", null)]
    [InlineData("video/mp4", "")]
    [InlineData("video/mp4", "video/quicktime")]
    [InlineData("video/mp4", "application/octet-stream")]
    public void ContentTypesMatch_RejectsMissingOrDifferentTypes(string declared, string? stored)
    {
        Assert.False(ExerciseVideoPolicy.ContentTypesMatch(declared, stored));
    }

    [Theory]
    [MemberData(nameof(Rejections))]
    public void Evaluate_ReturnsTheStableRejectionCode(
        string declaredContentType,
        VideoTechnicalMetadata metadata,
        string expectedCode)
    {
        Assert.Equal(expectedCode, ExerciseVideoPolicy.Evaluate(declaredContentType, metadata));
    }

    private static VideoTechnicalMetadata Metadata(
        VideoContainer container = VideoContainer.Mp4,
        long durationMilliseconds = 30_000,
        int width = 1280,
        int height = 720,
        string videoCodec = "avc1",
        string? audioCodec = "mp4a",
        int videoTracks = 1,
        int audioTracks = 1) =>
        new(container, durationMilliseconds, width, height, videoCodec, audioCodec, videoTracks, audioTracks);
}
