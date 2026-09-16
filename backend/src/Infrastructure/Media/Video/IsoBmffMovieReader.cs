using System.Buffers.Binary;
using System.Text;
using Application.Features.Training.ExerciseVideos.Abstractions;

namespace Infrastructure.Media.Video;

/// <summary>Leitor mínimo e defensivo de ISO BMFF (MP4) e QuickTime (MOV).</summary>
internal static class IsoBmffMovieReader
{
    internal const int MaxDepth = 8;
    internal const int MaxBoxes = 4096;
    internal const int MaxTracks = 16;
    private const long MaxDurationMilliseconds = 24L * 60 * 60 * 1000;

    private static readonly HashSet<string> Mp4Brands =
        new(StringComparer.Ordinal) { "isom", "iso2", "mp41", "mp42", "avc1", "M4V " };

    private static readonly HashSet<string> FragmentedTopLevelBoxes =
        new(StringComparer.Ordinal) { "moof", "mfra", "sidx", "styp", "emsg" };

    /// <summary>Indica se a box de topo denuncia um MP4 fragmentado.</summary>
    internal static bool IsFragmentedTopLevelBox(string type) => FragmentedTopLevelBoxes.Contains(type);

    /// <summary>
    /// Lê um cabeçalho de box na posição absoluta indicada. <paramref name="data"/>
    /// começa no primeiro byte da box.
    /// </summary>
    internal static bool TryReadHeader(
        ReadOnlySpan<byte> data,
        long absoluteOffset,
        long containerEnd,
        out BoxHeader header)
    {
        header = default;
        if (data.Length < 8 || absoluteOffset < 0 || absoluteOffset >= containerEnd)
            return false;

        var size32 = BinaryPrimitives.ReadUInt32BigEndian(data);
        var type = ReadFourCc(data.Slice(4, 4));
        if (type is null)
            return false;

        long size;
        var headerLength = 8;
        if (size32 == 1)
        {
            if (data.Length < 16)
                return false;

            var large = BinaryPrimitives.ReadUInt64BigEndian(data.Slice(8, 8));
            if (large < 16 || large > long.MaxValue)
                return false;

            size = (long)large;
            headerLength = 16;
        }
        else if (size32 == 0)
        {
            size = containerEnd - absoluteOffset;
        }
        else
        {
            if (size32 < 8)
                return false;

            size = size32;
        }

        if (type == "uuid")
            headerLength += 16;

        if (size < headerLength || size > containerEnd - absoluteOffset)
            return false;

        header = new BoxHeader(type, absoluteOffset, size, headerLength);
        return true;
    }

    /// <summary>Resolve o container a partir do conteúdo integral da box <c>ftyp</c>.</summary>
    internal static VideoContainer? ResolveContainer(ReadOnlySpan<byte> ftyp)
    {
        if (ftyp.Length < 16 || (ftyp.Length - 16) % 4 != 0)
            return null;

        var major = ReadFourCc(ftyp.Slice(8, 4));
        if (major == "qt  ")
            return VideoContainer.QuickTime;
        if (major is not null && Mp4Brands.Contains(major))
            return VideoContainer.Mp4;

        var compatible = new List<string>();
        for (var offset = 16; offset + 4 <= ftyp.Length; offset += 4)
        {
            if (ReadFourCc(ftyp.Slice(offset, 4)) is { } brand)
                compatible.Add(brand);
        }

        if (compatible.Contains("qt  "))
            return VideoContainer.QuickTime;

        return compatible.Any(Mp4Brands.Contains) ? VideoContainer.Mp4 : null;
    }

    /// <summary>Lê a box <c>moov</c> integral e extrai a metadata técnica.</summary>
    internal static MovieReadResult ReadMovie(ReadOnlySpan<byte> moov, VideoContainer container)
    {
        if (!TryReadHeader(moov, 0, moov.Length, out var root) || root.Type != "moov" ||
            root.Size != moov.Length)
            return MovieReadResult.Failure("exercise_video_container_malformed");

        var budget = new Budget();
        MovieHeader? movieHeader = null;
        var tracks = new List<TrackInfo>();
        var cursor = root.HeaderLength;

        while (cursor < moov.Length)
        {
            if (!budget.TryConsume() ||
                !TryReadHeader(moov[cursor..], cursor, moov.Length, out var child))
                return MovieReadResult.Failure("exercise_video_container_malformed");

            var body = moov.Slice(cursor, (int)child.Size);
            switch (child.Type)
            {
                case "mvex":
                    return MovieReadResult.Failure("exercise_video_fragmented_unsupported");

                case "mvhd":
                    movieHeader = ReadMovieHeader(body);
                    if (movieHeader is null)
                        return MovieReadResult.Failure("exercise_video_container_malformed");
                    break;

                case "trak":
                    if (tracks.Count >= MaxTracks)
                        return MovieReadResult.Failure("exercise_video_track_layout_unsupported");

                    var track = ReadTrack(body, child.HeaderLength, budget);
                    if (track is null)
                        return MovieReadResult.Failure("exercise_video_container_malformed");

                    tracks.Add(track);
                    break;
            }

            cursor += (int)child.Size;
        }

        if (movieHeader is null)
            return MovieReadResult.Failure("exercise_video_container_malformed");

        var videoTracks = tracks.Where(track => track.Enabled && track.Handler == "vide").ToList();
        var audioTracks = tracks.Where(track => track.Enabled && track.Handler == "soun").ToList();

        if (videoTracks.Count == 0)
            return MovieReadResult.Failure("exercise_video_track_layout_unsupported");

        var video = videoTracks[0];
        var durationMilliseconds = ComputeDurationMilliseconds(movieHeader, tracks);
        if (durationMilliseconds is null)
            return MovieReadResult.Failure("exercise_video_container_malformed");

        var (width, height) = video.RotatesQuarterTurn
            ? (video.Height, video.Width)
            : (video.Width, video.Height);

        if (width <= 0 || height <= 0 || width > 16384 || height > 16384)
            return MovieReadResult.Failure("exercise_video_container_malformed");

        return MovieReadResult.Success(new VideoTechnicalMetadata(
            container,
            durationMilliseconds.Value,
            width,
            height,
            video.Codec ?? "????",
            audioTracks.Count > 0 ? audioTracks[0].Codec : null,
            videoTracks.Count,
            audioTracks.Count));
    }

    private static MovieHeader? ReadMovieHeader(ReadOnlySpan<byte> box)
    {
        if (box.Length < 12)
            return null;

        var version = box[8];
        if (version == 0 && box.Length >= 108)
            return new MovieHeader(
                BinaryPrimitives.ReadUInt32BigEndian(box.Slice(20, 4)),
                BinaryPrimitives.ReadUInt32BigEndian(box.Slice(24, 4)),
                IsIndeterminate: BinaryPrimitives.ReadUInt32BigEndian(box.Slice(24, 4)) == uint.MaxValue);

        if (version == 1 && box.Length >= 120)
        {
            var duration = BinaryPrimitives.ReadUInt64BigEndian(box.Slice(32, 8));
            return new MovieHeader(
                BinaryPrimitives.ReadUInt32BigEndian(box.Slice(28, 4)),
                duration,
                IsIndeterminate: duration == ulong.MaxValue);
        }

        return null;
    }

    private static TrackInfo? ReadTrack(ReadOnlySpan<byte> trak, int headerLength, Budget budget)
    {
        TrackHeader? trackHeader = null;
        string? handler = null;
        string? codec = null;
        var cursor = headerLength;

        while (cursor < trak.Length)
        {
            if (!budget.TryConsume() ||
                !TryReadHeader(trak[cursor..], cursor, trak.Length, out var child))
                return null;

            var body = trak.Slice(cursor, (int)child.Size);
            if (child.Type == "tkhd")
            {
                trackHeader = ReadTrackHeader(body);
                if (trackHeader is null)
                    return null;
            }
            else if (child.Type == "mdia")
            {
                if (!ReadMedia(body, child.HeaderLength, budget, depth: 3, ref handler, ref codec))
                    return null;
            }

            cursor += (int)child.Size;
        }

        return trackHeader is null
            ? null
            : new TrackInfo(
                handler,
                codec,
                trackHeader.Enabled,
                trackHeader.Width,
                trackHeader.Height,
                trackHeader.RotatesQuarterTurn,
                trackHeader.Duration);
    }

    private static TrackHeader? ReadTrackHeader(ReadOnlySpan<byte> box)
    {
        if (box.Length < 12)
            return null;

        var version = box[8];
        var flags = (box[9] << 16) | (box[10] << 8) | box[11];

        int matrixOffset;
        int sizeOffset;
        ulong duration;

        if (version == 0 && box.Length >= 92)
        {
            duration = BinaryPrimitives.ReadUInt32BigEndian(box.Slice(28, 4));
            matrixOffset = 48;
            sizeOffset = 84;
        }
        else if (version == 1 && box.Length >= 104)
        {
            duration = BinaryPrimitives.ReadUInt64BigEndian(box.Slice(36, 8));
            matrixOffset = 60;
            sizeOffset = 96;
        }
        else
        {
            return null;
        }

        // Matriz {a, b, u, c, d, v, x, y, w}: a, b, c e d em 16.16. Uma rotação de
        // 90 ou 270 graus põe o peso fora da diagonal e troca largura com altura.
        var a = Math.Abs((long)BinaryPrimitives.ReadInt32BigEndian(box.Slice(matrixOffset, 4)));
        var b = Math.Abs((long)BinaryPrimitives.ReadInt32BigEndian(box.Slice(matrixOffset + 4, 4)));
        var c = Math.Abs((long)BinaryPrimitives.ReadInt32BigEndian(box.Slice(matrixOffset + 12, 4)));
        var d = Math.Abs((long)BinaryPrimitives.ReadInt32BigEndian(box.Slice(matrixOffset + 16, 4)));

        return new TrackHeader(
            Enabled: (flags & 0x1) == 0x1,
            Width: (int)(BinaryPrimitives.ReadUInt32BigEndian(box.Slice(sizeOffset, 4)) >> 16),
            Height: (int)(BinaryPrimitives.ReadUInt32BigEndian(box.Slice(sizeOffset + 4, 4)) >> 16),
            RotatesQuarterTurn: b + c > a + d,
            Duration: duration);
    }

    /// <summary>Desce mdia → minf → stbl → stsd lendo apenas o handler e o primeiro fourcc.</summary>
    private static bool ReadMedia(
        ReadOnlySpan<byte> container,
        int headerLength,
        Budget budget,
        int depth,
        ref string? handler,
        ref string? codec)
    {
        if (depth > MaxDepth)
            return false;

        var cursor = headerLength;
        while (cursor < container.Length)
        {
            if (!budget.TryConsume() ||
                !TryReadHeader(container[cursor..], cursor, container.Length, out var child))
                return false;

            var body = container.Slice(cursor, (int)child.Size);
            switch (child.Type)
            {
                // Só o hdlr filho direto de mdia identifica a track; o de minf
                // (QuickTime) descreve a referência de dados e é ignorado.
                case "hdlr" when depth == 3:
                    if (body.Length < 32)
                        return false;
                    handler = ReadFourCc(body.Slice(16, 4));
                    break;

                case "minf":
                case "stbl":
                    if (!ReadMedia(body, child.HeaderLength, budget, depth + 1, ref handler, ref codec))
                        return false;
                    break;

                case "stsd":
                    if (!TryReadFirstSampleEntry(body, out codec))
                        return false;
                    break;
            }

            cursor += (int)child.Size;
        }

        return true;
    }

    private static bool TryReadFirstSampleEntry(ReadOnlySpan<byte> stsd, out string? codec)
    {
        codec = null;
        if (stsd.Length < 16)
            return false;

        var entryCount = BinaryPrimitives.ReadUInt32BigEndian(stsd.Slice(12, 4));
        if (entryCount is 0 or > 16)
            return false;

        if (stsd.Length < 24)
            return false;

        var entrySize = BinaryPrimitives.ReadUInt32BigEndian(stsd.Slice(16, 4));
        if (entrySize < 16 || entrySize > stsd.Length - 16)
            return false;

        codec = ReadFourCc(stsd.Slice(20, 4)) ?? "????";
        return true;
    }

    private static long? ComputeDurationMilliseconds(MovieHeader header, List<TrackInfo> tracks)
    {
        if (header.Timescale == 0)
            return null;

        var duration = header.Duration;
        if (duration == 0 || header.IsIndeterminate)
            duration = tracks.Count == 0 ? 0 : tracks.Max(track => track.Duration);

        if (duration == 0 || duration == uint.MaxValue || duration == ulong.MaxValue)
            return null;

        var milliseconds = (decimal)duration * 1000m / header.Timescale;
        return milliseconds is <= 0 or > MaxDurationMilliseconds
            ? null
            : (long)Math.Floor(milliseconds);
    }

    /// <summary>
    /// Devolve o fourcc só quando é ASCII imprimível: bytes arbitrários do ficheiro
    /// nunca chegam a logs nem à base de dados.
    /// </summary>
    private static string? ReadFourCc(ReadOnlySpan<byte> bytes)
    {
        foreach (var value in bytes)
        {
            if (value is < 0x20 or > 0x7E)
                return null;
        }

        return Encoding.ASCII.GetString(bytes);
    }

    internal readonly record struct BoxHeader(string Type, long Offset, long Size, int HeaderLength);

    internal sealed record MovieReadResult(VideoTechnicalMetadata? Metadata, string? FailureCode)
    {
        internal static MovieReadResult Success(VideoTechnicalMetadata metadata) => new(metadata, null);
        internal static MovieReadResult Failure(string failureCode) => new(null, failureCode);
    }

    private sealed record MovieHeader(uint Timescale, ulong Duration, bool IsIndeterminate);

    private sealed record TrackHeader(
        bool Enabled,
        int Width,
        int Height,
        bool RotatesQuarterTurn,
        ulong Duration);

    private sealed record TrackInfo(
        string? Handler,
        string? Codec,
        bool Enabled,
        int Width,
        int Height,
        bool RotatesQuarterTurn,
        ulong Duration);

    private sealed class Budget
    {
        private int _remaining = MaxBoxes;

        public bool TryConsume() => --_remaining >= 0;
    }
}
