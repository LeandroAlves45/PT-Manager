using Application.Features.Training.ExerciseVideos.Abstractions;
using Infrastructure.Media.Video;
using Microsoft.Extensions.Logging.Abstractions;
using static Infrastructure.IntegrationTests.Media.Video.Mp4TestFile;

namespace Infrastructure.IntegrationTests.Media.Video;

/// <summary>
/// Prova que o probe lê só cabeçalhos por intervalos, encontra o moov no início ou
/// no fim, exige o ETag confirmado e recusa estruturas não suportadas.
/// </summary>
public sealed class VideoMetadataProbeTests
{
    private const string Key = "exercise-videos/global/0123456789abcdef0123456789abcdef";
    private const string ETag = "etag-accepted";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Probe_WhenMovieIsAtTheStart_UsesASingleRead()
    {
        var file = File(StandardMoov(), moovAtEnd: false);
        var reader = new ScriptedRangeReader(file);

        var outcome = await Probe(reader).ProbeAsync(Key, null, file.Length, ETag, Token);

        Assert.Equal(VideoProbeStatus.Probed, outcome.Status);
        Assert.Equal((VideoContainer.Mp4, "avc1"), (outcome.Metadata!.Container, outcome.Metadata.VideoCodec));
        Assert.Single(reader.Reads);
    }

    [Fact]
    public async Task Probe_WhenMovieIsAtTheEnd_FollowsHeadersInsteadOfDownloading()
    {
        var file = File(StandardMoov(), moovAtEnd: true, mediaBytes: 5 * 1024 * 1024);
        var reader = new ScriptedRangeReader(file);

        var outcome = await Probe(reader).ProbeAsync(Key, null, file.Length, ETag, Token);

        Assert.Equal(VideoProbeStatus.Probed, outcome.Status);
        Assert.InRange(reader.Reads.Count, 2, 4);
        Assert.True(reader.Reads.Sum(read => (long)read.Length) < 200_000);
    }

    [Fact]
    public async Task Probe_ForQuickTimeBrand_ReportsTheQuickTimeContainer()
    {
        var file = File(StandardMoov(), moovAtEnd: true, majorBrand: "qt  ");

        var outcome = await Probe(new ScriptedRangeReader(file)).ProbeAsync(Key, null, file.Length, ETag, Token);

        Assert.Equal(VideoContainer.QuickTime, outcome.Metadata!.Container);
    }

    [Fact]
    public async Task Probe_SendsTheConfirmedETagOnEveryRead()
    {
        var file = File(StandardMoov(), moovAtEnd: true, mediaBytes: 200_000);
        var reader = new ScriptedRangeReader(file);

        await Probe(reader).ProbeAsync(Key, null, file.Length, ETag, Token);

        Assert.All(reader.Reads, read => Assert.Equal(ETag, read.ETag));
    }

    [Fact]
    public async Task Probe_WhenFirstBoxIsNotFileType_RefusesTheContainer()
    {
        byte[] file = [.. StandardMoov(), .. Box("mdat", new byte[64])];

        var outcome = await Probe(new ScriptedRangeReader(file)).ProbeAsync(Key, null, file.Length, ETag, Token);

        Assert.Equal("exercise_video_container_unsupported", outcome.FailureCode);
    }

    [Fact]
    public async Task Probe_WhenTopLevelContainsMovieFragments_RefusesThem()
    {
        byte[] file = [.. Ftyp("isom"), .. Box("moof", new byte[16]), .. StandardMoov()];

        var outcome = await Probe(new ScriptedRangeReader(file)).ProbeAsync(Key, null, file.Length, ETag, Token);

        Assert.Equal("exercise_video_fragmented_unsupported", outcome.FailureCode);
    }

    [Fact]
    public async Task Probe_WhenMovieBoxIsTooLarge_RefusesBeforeReadingIt()
    {
        var hugeMoov = Box("moov", new byte[VideoMetadataProbe.MaxMovieBytes + 1]);
        byte[] file = [.. Ftyp("isom"), .. Box("mdat", new byte[VideoMetadataProbe.InitialReadLength]), .. hugeMoov];
        var reader = new ScriptedRangeReader(file);

        var outcome = await Probe(reader).ProbeAsync(Key, null, file.Length, ETag, Token);

        Assert.Equal("exercise_video_metadata_too_large", outcome.FailureCode);
        Assert.DoesNotContain(reader.Reads, read => read.Length > VideoMetadataProbe.InitialReadLength);
    }

    [Fact]
    public async Task Probe_WhenFileHasNoMovie_RefusesIt()
    {
        byte[] file = [.. Ftyp("isom"), .. Box("mdat", new byte[128])];

        var outcome = await Probe(new ScriptedRangeReader(file)).ProbeAsync(Key, null, file.Length, ETag, Token);

        Assert.Equal("exercise_video_movie_missing", outcome.FailureCode);
    }

    // O estado de leitura é interno à Infrastructure; o teste recebe o nome para
    // manter a assinatura pública exigida pelo xUnit.
    [Theory]
    [InlineData(nameof(VideoRangeReadStatus.Changed), VideoProbeStatus.ObjectChanged)]
    [InlineData(nameof(VideoRangeReadStatus.NotFound), VideoProbeStatus.NotFound)]
    [InlineData(nameof(VideoRangeReadStatus.Disabled), VideoProbeStatus.Disabled)]
    [InlineData(nameof(VideoRangeReadStatus.TransientFailure), VideoProbeStatus.TransientFailure)]
    [InlineData(nameof(VideoRangeReadStatus.PermanentFailure), VideoProbeStatus.Unsupported)]
    public async Task Probe_ClassifiesReadFailures(string readStatus, VideoProbeStatus expected)
    {
        var file = File(StandardMoov(), moovAtEnd: false);
        var reader = new ScriptedRangeReader(file) { ForcedStatus = Enum.Parse<VideoRangeReadStatus>(readStatus) };

        var outcome = await Probe(reader).ProbeAsync(Key, null, file.Length, ETag, Token);

        Assert.Equal(expected, outcome.Status);
    }

    [Fact]
    public async Task Probe_WhenProviderReturnsFewerBytes_TreatsTheFileAsMalformed()
    {
        var file = File(StandardMoov(), moovAtEnd: true, mediaBytes: 200_000);
        var reader = new ScriptedRangeReader(file) { TruncateAfterFirstRead = true };

        var outcome = await Probe(reader).ProbeAsync(Key, null, file.Length, ETag, Token);

        Assert.Equal("exercise_video_container_malformed", outcome.FailureCode);
    }

    [Fact]
    public async Task Probe_WhenFileIsTooSmall_RefusesWithoutReading()
    {
        var reader = new ScriptedRangeReader(new byte[8]);

        var outcome = await Probe(reader).ProbeAsync(Key, null, 8, ETag, Token);

        Assert.Equal("exercise_video_container_malformed", outcome.FailureCode);
        Assert.Empty(reader.Reads);
    }

    private static VideoMetadataProbe Probe(ScriptedRangeReader reader) =>
        new(reader, NullLogger<VideoMetadataProbe>.Instance);

    private sealed class ScriptedRangeReader(byte[] file) : IVideoObjectRangeReader
    {
        public List<(long Offset, int Length, string ETag)> Reads { get; } = [];
        public VideoRangeReadStatus? ForcedStatus { get; init; }
        public bool TruncateAfterFirstRead { get; init; }

        public Task<VideoRangeReadOutcome> ReadRangeAsync(
            string objectKey, Guid? ownerTrainerId, long offset, int length, string eTag,
            CancellationToken cancellationToken)
        {
            Reads.Add((offset, length, eTag));

            if (ForcedStatus is { } status)
                return Task.FromResult(new VideoRangeReadOutcome(status));

            var available = (int)Math.Min(length, file.Length - offset);
            if (TruncateAfterFirstRead && Reads.Count > 1)
                available = Math.Max(0, available - 1);

            return Task.FromResult(new VideoRangeReadOutcome(
                VideoRangeReadStatus.Success,
                file.AsSpan((int)offset, available).ToArray()));
        }
    }
}
