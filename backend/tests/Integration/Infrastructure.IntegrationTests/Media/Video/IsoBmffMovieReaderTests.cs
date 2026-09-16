using System.Buffers.Binary;
using Application.Features.Training.ExerciseVideos.Abstractions;
using Infrastructure.Media.Video;
using static Infrastructure.IntegrationTests.Media.Video.Mp4TestFile;

namespace Infrastructure.IntegrationTests.Media.Video;

/// <summary>
/// Prova a leitura estrutural de MP4 e MOV e a recusa determinística de
/// estruturas malformadas, fragmentadas ou maliciosas.
/// </summary>
public sealed class IsoBmffMovieReaderTests
{
    [Fact]
    public void ReadMovie_ExtractsContainerCodecsDurationAndResolution()
    {
        var result = IsoBmffMovieReader.ReadMovie(StandardMoov(), VideoContainer.Mp4);

        Assert.Equal(
            new VideoTechnicalMetadata(VideoContainer.Mp4, 12_500, 1280, 720, "avc1", "mp4a", 1, 1),
            result.Metadata);
    }

    [Fact]
    public void ReadMovie_SwapsDimensionsForAQuarterTurnRotation()
    {
        var moov = Box("moov", Mvhd(600, 6000), Trak("vide", "avc1", 1920, 1080, rotate90: true));

        var metadata = IsoBmffMovieReader.ReadMovie(moov, VideoContainer.QuickTime).Metadata!;

        Assert.Equal((1080, 1920, 10_000L, (string?)null), (metadata.Width, metadata.Height, metadata.DurationMilliseconds, metadata.AudioCodec));
    }

    [Fact]
    public void ReadMovie_IgnoresDisabledAndNonMediaTracks()
    {
        var moov = Box("moov",
            Mvhd(1000, 5000),
            Trak("vide", "avc1", 1280, 720),
            Trak("vide", "hvc1", 3840, 2160, enabled: false),
            Trak("soun", "mp4a", enabled: false),
            Trak("meta", "mebx"));

        var metadata = IsoBmffMovieReader.ReadMovie(moov, VideoContainer.Mp4).Metadata!;

        Assert.Equal((1, 0, "avc1"), (metadata.VideoTrackCount, metadata.AudioTrackCount, metadata.VideoCodec));
    }

    [Fact]
    public void ReadMovie_WhenMovieExtendsIsPresent_RefusesFragmentedFiles()
    {
        var result = IsoBmffMovieReader.ReadMovie(StandardMoov(Box("mvex")), VideoContainer.Mp4);

        Assert.Equal("exercise_video_fragmented_unsupported", result.FailureCode);
    }

    [Fact]
    public void ReadMovie_WithoutVideoTrack_RefusesTheLayout()
    {
        var moov = Box("moov", Mvhd(1000, 5000), Trak("soun", "mp4a"));

        Assert.Equal(
            "exercise_video_track_layout_unsupported",
            IsoBmffMovieReader.ReadMovie(moov, VideoContainer.Mp4).FailureCode);
    }

    [Fact]
    public void ReadMovie_WithZeroTimescale_IsMalformed()
    {
        var moov = Box("moov", Mvhd(0, 5000), Trak("vide", "avc1", 1280, 720));

        Assert.Equal(
            "exercise_video_container_malformed",
            IsoBmffMovieReader.ReadMovie(moov, VideoContainer.Mp4).FailureCode);
    }

    [Fact]
    public void ReadMovie_WhenMovieDurationIsZero_FallsBackToTheLongestTrack()
    {
        var moov = Box("moov", Mvhd(1000, 0), Trak("vide", "avc1", 1280, 720, duration: 7_000));

        Assert.Equal(7_000L, IsoBmffMovieReader.ReadMovie(moov, VideoContainer.Mp4).Metadata!.DurationMilliseconds);
    }

    [Fact]
    public void ReadMovie_WhenAChildClaimsMoreBytesThanItsParent_IsMalformed()
    {
        var moov = StandardMoov();
        // O primeiro filho (mvhd) passa a declarar 4 GiB dentro de um moov pequeno.
        BinaryPrimitives.WriteUInt32BigEndian(moov.AsSpan(8), uint.MaxValue);

        Assert.Equal(
            "exercise_video_container_malformed",
            IsoBmffMovieReader.ReadMovie(moov, VideoContainer.Mp4).FailureCode);
    }

    [Fact]
    public void ReadMovie_WhenABoxDeclaresLessThanItsHeader_IsMalformed()
    {
        var moov = StandardMoov();
        BinaryPrimitives.WriteUInt32BigEndian(moov.AsSpan(8), 7);

        Assert.Equal(
            "exercise_video_container_malformed",
            IsoBmffMovieReader.ReadMovie(moov, VideoContainer.Mp4).FailureCode);
    }

    [Fact]
    public void ReadMovie_WhenBoxCountExceedsTheBudget_RefusesInsteadOfLooping()
    {
        var tinyBoxes = Enumerable.Range(0, IsoBmffMovieReader.MaxBoxes + 1).Select(_ => Box("free")).ToArray();
        var moov = Box("moov", new[] { Mvhd(1000, 5000), Trak("vide", "avc1", 1280, 720) }.Concat(tinyBoxes).ToArray());

        Assert.Equal(
            "exercise_video_container_malformed",
            IsoBmffMovieReader.ReadMovie(moov, VideoContainer.Mp4).FailureCode);
    }

    [Fact]
    public void ReadMovie_WhenCodecIsNotPrintable_NeverReturnsRawBytes()
    {
        var moov = Box("moov", Mvhd(1000, 5000), Trak("vide", [0x00, 0xFF, 0x0A, 0x41], 1280, 720, false, true, 5000));

        Assert.Equal("????", IsoBmffMovieReader.ReadMovie(moov, VideoContainer.Mp4).Metadata!.VideoCodec);
    }

    [Theory]
    [InlineData("qt  ", new string[0], VideoContainer.QuickTime)]
    [InlineData("isom", new string[0], VideoContainer.Mp4)]
    [InlineData("mp42", new string[0], VideoContainer.Mp4)]
    [InlineData("M4V ", new string[0], VideoContainer.Mp4)]
    [InlineData("3gp4", new[] { "isom" }, VideoContainer.Mp4)]
    [InlineData("M4A ", new[] { "qt  " }, VideoContainer.QuickTime)]
    [InlineData("dash", new[] { "iso6" }, null)]
    [InlineData("heic", new[] { "mif1" }, null)]
    public void ResolveContainer_UsesAClosedBrandAllowlist(
        string majorBrand, string[] compatibleBrands, VideoContainer? expected)
    {
        Assert.Equal(expected, IsoBmffMovieReader.ResolveContainer(Ftyp(majorBrand, compatibleBrands)));
    }

    [Fact]
    public void ResolveContainer_WhenBrandListIsMisaligned_ReturnsNull()
    {
        byte[] ftyp = [.. Ftyp("isom"), 0x61];
        BinaryPrimitives.WriteUInt32BigEndian(ftyp, (uint)ftyp.Length);

        Assert.Null(IsoBmffMovieReader.ResolveContainer(ftyp));
    }

    [Fact]
    public void TryReadHeader_HandlesLargeSizeAndSizeZero()
    {
        var large = new byte[16];
        BinaryPrimitives.WriteUInt32BigEndian(large, 1);
        "mdat"u8.CopyTo(large.AsSpan(4));
        BinaryPrimitives.WriteUInt64BigEndian(large.AsSpan(8), 5_000_000_000);

        Assert.True(IsoBmffMovieReader.TryReadHeader(large, 100, 6_000_000_000, out var header));
        Assert.Equal((5_000_000_000L, 16), (header.Size, header.HeaderLength));

        var toEnd = new byte[8];
        "mdat"u8.CopyTo(toEnd.AsSpan(4));
        Assert.True(IsoBmffMovieReader.TryReadHeader(toEnd, 100, 1_000, out header));
        Assert.Equal(900L, header.Size);

        BinaryPrimitives.WriteUInt64BigEndian(large.AsSpan(8), 15);
        Assert.False(IsoBmffMovieReader.TryReadHeader(large, 0, 1_000, out _));
    }
}
