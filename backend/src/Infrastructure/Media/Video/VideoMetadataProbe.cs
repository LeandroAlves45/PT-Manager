using Application.Features.Training.ExerciseVideos.Abstractions;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Media.Video;

/// <summary>Leitura parcial de um objeto de vídeo armazenado.</summary>
internal interface IVideoObjectRangeReader
{
    /// <summary>
    /// Lê exatamente o length de bytes a partir de
    /// offset, só se o objeto ainda tiver o ETag indicado.
    /// </summary>
    Task<VideoRangeReadOutcome> ReadRangeAsync(
        string objectKey,
        Guid? ownerTrainerId,
        long offset,
        int length,
        string eTag,
        CancellationToken cancellationToken);
}

internal enum VideoRangeReadStatus
{
    Success,
    NotFound,
    Changed,
    Disabled,
    TransientFailure,
    PermanentFailure
}

internal sealed record VideoRangeReadOutcome(
    VideoRangeReadStatus Status,
    byte[]? Data = null,
    string? FailureCode = null);

/// <summary>
/// Extrai a metadata técnica percorrendo as boxes de topo por leituras parciais,
/// sem descarregar o ficheiro nem depender de ffmpeg.
/// </summary>
/// <remarks>
/// Telemóveis gravam o <c>moov</c> no início ou no fim. O probe segue a cadeia de
/// cabeçalhos em vez de procurar a sequência "moov" por força bruta, com um teto
/// de leituras e de boxes. Cada leitura exige o ETag confirmado na finalização.
/// </remarks>
internal sealed class VideoMetadataProbe : IVideoMetadataProbe
{
    internal const int InitialReadLength = 64 * 1024;
    internal const int MaxMovieBytes = 8 * 1024 * 1024;
    internal const int MaxFileTypeBytes = 4096;
    internal const int MaxTopLevelBoxes = 32;
    private const int HeaderReadLength = 32;

    private readonly IVideoObjectRangeReader _reader;
    private readonly ILogger<VideoMetadataProbe> _logger;

    public VideoMetadataProbe(IVideoObjectRangeReader reader, ILogger<VideoMetadataProbe> logger)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<VideoProbeOutcome> ProbeAsync(
        string objectKey,
        Guid? ownerTrainerId,
        long sizeBytes,
        string eTag,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(eTag);

        if (sizeBytes < 16)
            return Unsupported("exercise_video_container_malformed");

        var initialLength = (int)Math.Min(InitialReadLength, sizeBytes);
        var initial = await _reader.ReadRangeAsync(
            objectKey,
            ownerTrainerId,
            0,
            initialLength,
            eTag,
            cancellationToken);
        if (initial.Status != VideoRangeReadStatus.Success)
            return MapReadFailure(initial);

        var buffer = initial.Data!;
        VideoContainer? container = null;
        long cursor = 0;

        for (var index = 0; index < MaxTopLevelBoxes; index++)
        {
            if (cursor >= sizeBytes)
                return Unsupported("exercise_video_movie_missing");

            var headerBytes = await ReadAsync(
                objectKey,
                ownerTrainerId,
                buffer,
                cursor,
                (int)Math.Min(HeaderReadLength, sizeBytes - cursor),
                eTag,
                cancellationToken);
            if (headerBytes.Failure is not null)
                return headerBytes.Failure;

            if (!IsoBmffMovieReader.TryReadHeader(
                headerBytes.Data, cursor, sizeBytes, out var box))
                return Unsupported("exercise_video_container_malformed");

            // O primeiro box tem de declarar o tipo: um MOV antigo sem ftyp é recusado.
            if (index == 0 && box.Type != "ftyp")
                return Unsupported("exercise_video_container_unsupported");

            if (IsoBmffMovieReader.IsFragmentedTopLevelBox(box.Type))
                return Unsupported("exercise_video_fragmented_unsupported");

            if (box.Type == "ftyp")
            {
                if (box.Size > MaxFileTypeBytes)
                    return Unsupported("exercise_video_container_malformed");

                var ftyp = await ReadAsync(
                    objectKey,
                    ownerTrainerId,
                    buffer,
                    cursor,
                    (int)box.Size,
                    eTag,
                    cancellationToken);
                if (ftyp.Failure is not null)
                    return ftyp.Failure;

                container = IsoBmffMovieReader.ResolveContainer(ftyp.Data);
                if (container is null)
                    return Unsupported("exercise_video_container_unsupported");
            }
            else if (box.Type == "moov")
            {
                if (box.Size > MaxMovieBytes)
                    return Unsupported("exercise_video_metadata_too_large");

                var moov = await ReadAsync(
                    objectKey,
                    ownerTrainerId,
                    buffer,
                    cursor,
                    (int)box.Size,
                    eTag,
                    cancellationToken);
                if (moov.Failure is not null)
                    return moov.Failure;

                var movie = IsoBmffMovieReader.ReadMovie(moov.Data, container!.Value);
                return movie.Metadata is null
                    ? Unsupported(movie.FailureCode!)
                    : VideoProbeOutcome.Probed(movie.Metadata);
            }

            cursor += box.Size;
        }

        return Unsupported("exercise_video_container_malformed");
    }

    /// <summary>Serve a leitura do buffer inicial quando possível; caso contrário, lê o intervalo.</summary>
    private async Task<RangeBytes> ReadAsync(
        string objectKey,
        Guid? ownerTrainerId,
        byte[] buffer,
        long offset,
        int length,
        string eTag,
        CancellationToken cancellationToken)
    {
        if (offset + length <= buffer.Length)
            return new RangeBytes(buffer.AsMemory((int)offset, length), null);

        var read = await _reader.ReadRangeAsync(
            objectKey,
            ownerTrainerId,
            offset,
            length,
            eTag,
            cancellationToken);

        if (read.Status != VideoRangeReadStatus.Success)
            return new RangeBytes(ReadOnlyMemory<byte>.Empty, MapReadFailure(read));

        return read.Data!.Length == length
            ? new RangeBytes(read.Data, null)
            : new RangeBytes(
                ReadOnlyMemory<byte>.Empty, Unsupported("exercise_video_container_malformed"));
    }

    private VideoProbeOutcome Unsupported(string failureCode)
    {
        _logger.LogWarning(
            VideoLogEvents.ProbeUnsupported,
            "Stored exercise video was refused by the container probe with {FailureCode}",
            failureCode);

        return VideoProbeOutcome.Failure(VideoProbeStatus.Unsupported, failureCode);
    }

    private VideoProbeOutcome MapReadFailure(VideoRangeReadOutcome outcome)
    {
        switch (outcome.Status)
        {
            case VideoRangeReadStatus.Changed:
                _logger.LogWarning(
                    VideoLogEvents.ProbeObjectChanged,
                    "Stored exercise video changed after upload completion.");
                return VideoProbeOutcome.Failure(
                    VideoProbeStatus.ObjectChanged, "exercise_video_object_changed");

            case VideoRangeReadStatus.NotFound:
                return VideoProbeOutcome.Failure(
                    VideoProbeStatus.NotFound, "exercise_video_object_missing");

            case VideoRangeReadStatus.Disabled:
                return VideoProbeOutcome.Failure(
                    VideoProbeStatus.Disabled, "exercise_video_storage_disabled");

            case VideoRangeReadStatus.TransientFailure:
                return VideoProbeOutcome.Failure(
                    VideoProbeStatus.TransientFailure,
                    outcome.FailureCode ?? "exercise_video_storage_transient");

            default:
                return VideoProbeOutcome.Failure(
                    VideoProbeStatus.Unsupported,
                    outcome.FailureCode ?? "exercise_video_storage_refused");
        }
    }

    private readonly record struct RangeBytes(ReadOnlyMemory<byte> Memory, VideoProbeOutcome? Failure)
    {
        public ReadOnlySpan<byte> Data => Memory.Span;
    }
}
