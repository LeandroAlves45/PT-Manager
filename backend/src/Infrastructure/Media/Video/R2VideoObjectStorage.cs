using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Application.Common.Abstractions;
using Application.Features.Training.ExerciseVideos.Abstractions;
using Domain.Entities.Training;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Media.Video;

/// <summary>
/// Implementa o storage privado de vídeos e as leituras parciais sobre Cloudflare R2.
/// </summary>
/// <remarks>
/// <para>
/// Nenhuma operação aceita um identificador fora do espaço do owner confiável:
/// a validação acontece antes de qualquer I/O e usa o mesmo cálculo que gera o
/// identificador no Domain.
/// </para>
/// <para>
/// Os logs registam apenas operação e código HTTP. Identificadores, URLs
/// assinadas, credenciais e corpos de resposta nunca são escritos.
/// </para>
/// </remarks>
internal sealed class R2VideoObjectStorage : IVideoObjectStorage, IVideoObjectRangeReader
{
    private static readonly TimeSpan MaxPresignLifetime = TimeSpan.FromDays(7);
    private const int MaxETagLength = 128;

    private readonly R2ClientProvider _clientProvider;
    private readonly R2Options _options;
    private readonly IClock _clock;
    private readonly ILogger<R2VideoObjectStorage> _logger;

    public R2VideoObjectStorage(
        R2ClientProvider clientProvider,
        IOptions<R2Options> options,
        IClock clock,
        ILogger<R2VideoObjectStorage> logger)
    {
        _clientProvider = clientProvider ?? throw new ArgumentNullException(nameof(clientProvider));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<VideoUploadAuthorizationOutcome> CreateUploadAuthorizationAsync(
        string objectKey,
        Guid? ownerTrainerId,
        string contentType,
        long contentLength,
        DateTime expiresAt,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
            return new VideoUploadAuthorizationOutcome(VideoStorageStatus.Disabled);

        if (!TryResolveKey(objectKey, ownerTrainerId, out var key))
            return new VideoUploadAuthorizationOutcome(
                VideoStorageStatus.PermanentFailure, FailureCode: "r2_object_key_not_owned");

        if (contentLength <= 0 || !IsPresignLifetimeValid(expiresAt))
            return new VideoUploadAuthorizationOutcome(
                VideoStorageStatus.PermanentFailure, FailureCode: "r2_presign_request_invalid");

        var request = new GetPreSignedUrlRequest
        {
            BucketName = _options.BucketName,
            Key = key,
            Verb = HttpVerb.PUT,
            Protocol = Protocol.HTTPS,
            Expires = expiresAt,
            ContentType = contentType
        };

        // Defesa adicional: o R2 impõe o Content-Type assinado mas não garante impor
        // o Content-Length. A finalização confirma o tamanho real no fornecedor.
        request.Headers.ContentLength = contentLength;

        var url = await PresignAsync(request, "presign_put");
        return url is null
            ? new VideoUploadAuthorizationOutcome(
                VideoStorageStatus.PermanentFailure, FailureCode: "r2_presign_failed")
            : new VideoUploadAuthorizationOutcome(
                VideoStorageStatus.Success,
                new VideoUploadAuthorization(url, "PUT", contentType, contentLength, expiresAt));
    }

    public async Task<VideoObjectInfoOutcome> GetObjectInfoAsync(
        string objectKey,
        Guid? ownerTrainerId,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
            return new VideoObjectInfoOutcome(VideoStorageStatus.Disabled);

        if (!TryResolveKey(objectKey, ownerTrainerId, out var key))
            return new VideoObjectInfoOutcome(
                VideoStorageStatus.PermanentFailure, FailureCode: "r2_object_key_not_owned");

        var result = await SendAsync(
            "head",
            async token =>
            {
                var response = await _clientProvider.Client.GetObjectMetadataAsync(
                    new GetObjectMetadataRequest { BucketName = _options.BucketName, Key = key },
                    token);

                var eTag = NormalizeETag(response.ETag);
                return eTag is null || response.Headers.ContentLength < 0
                    ? null
                    : new VideoObjectInfo(
                        response.Headers.ContentLength, eTag, response.Headers.ContentType);
            },
            cancellationToken);

        return result.Status switch
        {
            VideoStorageStatus.Success when result.Value is not null =>
                new VideoObjectInfoOutcome(VideoStorageStatus.Success, result.Value),
            VideoStorageStatus.Success =>
                new VideoObjectInfoOutcome(
                    VideoStorageStatus.TransientFailure, FailureCode: "r2_invalid_response"),
            _ => new VideoObjectInfoOutcome(result.Status, FailureCode: result.FailureCode)
        };
    }

    public async Task<VideoRangeReadOutcome> ReadRangeAsync(
        string objectKey,
        Guid? ownerTrainerId,
        long offset,
        int length,
        string eTag,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
            return new VideoRangeReadOutcome(VideoRangeReadStatus.Disabled);

        if (!TryResolveKey(objectKey, ownerTrainerId, out var key))
            return new VideoRangeReadOutcome(
                VideoRangeReadStatus.PermanentFailure, FailureCode: "r2_object_key_not_owned");

        if (offset < 0 || length <= 0 ||
            length > VideoMetadataProbe.MaxMovieBytes || NormalizeETag(eTag) is null)
            return new VideoRangeReadOutcome(
                VideoRangeReadStatus.PermanentFailure, FailureCode: "r2_range_request_invalid");

        var result = await SendAsync(
            "range_get",
            async token =>
            {
                using var response = await _clientProvider.Client.GetObjectAsync(
                    new GetObjectRequest
                    {
                        BucketName = _options.BucketName,
                        Key = key,
                        ByteRange = new ByteRange(offset, offset + length - 1),
                        // O objeto tem de continuar a ser aceite na finalização.
                        EtagToMatch = $"\"{eTag}\""
                    },
                    token);

                return await ReadExactlyAsync(response.ResponseStream, length, token);
            },
            cancellationToken);

        return result.Status switch
        {
            VideoStorageStatus.Success when result.Value is not null =>
                new VideoRangeReadOutcome(VideoRangeReadStatus.Success, result.Value),
            VideoStorageStatus.Success =>
                new VideoRangeReadOutcome(
                    VideoRangeReadStatus.PermanentFailure, FailureCode: "r2_range_truncated"),
            VideoStorageStatus.NotFound => new VideoRangeReadOutcome(VideoRangeReadStatus.NotFound),
            VideoStorageStatus.TransientFailure =>
                new VideoRangeReadOutcome(
                    VideoRangeReadStatus.TransientFailure, FailureCode: result.FailureCode),
            _ when result.HttpStatus == HttpStatusCode.PreconditionFailed =>
                new VideoRangeReadOutcome(VideoRangeReadStatus.Changed),
            _ => new VideoRangeReadOutcome(
                VideoRangeReadStatus.PermanentFailure, FailureCode: result.FailureCode)
        };
    }

    public async Task<VideoPlaybackUrlOutcome> CreatePlaybackUrlAsync(
        string objectKey,
        Guid? ownerTrainerId,
        DateTime expiresAt,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
            return new VideoPlaybackUrlOutcome(VideoStorageStatus.Disabled);

        if (!TryResolveKey(objectKey, ownerTrainerId, out var key))
            return new VideoPlaybackUrlOutcome(
                VideoStorageStatus.PermanentFailure, FailureCode: "r2_object_key_not_owned");

        if (!IsPresignLifetimeValid(expiresAt))
            return new VideoPlaybackUrlOutcome(
                VideoStorageStatus.PermanentFailure, FailureCode: "r2_presign_request_invalid");

        var url = await PresignAsync(
            new GetPreSignedUrlRequest
            {
                BucketName = _options.BucketName,
                Key = key,
                Verb = HttpVerb.GET,
                Protocol = Protocol.HTTPS,
                Expires = expiresAt
            },
            "presign_get");

        return url is null
            ? new VideoPlaybackUrlOutcome(
                VideoStorageStatus.PermanentFailure, FailureCode: "r2_presign_failed")
            : new VideoPlaybackUrlOutcome(
                VideoStorageStatus.Success,
                new VideoPlaybackUrl(url, expiresAt));
    }

    public async Task<VideoObjectDeletionOutcome> DeleteAsync(
        string objectKey,
        Guid? ownerTrainerId,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
            return new VideoObjectDeletionOutcome(VideoStorageStatus.Disabled);

        if (!TryResolveKey(objectKey, ownerTrainerId, out var key))
            return new VideoObjectDeletionOutcome(
                VideoStorageStatus.PermanentFailure, "r2_object_key_not_owned");

        var result = await SendAsync(
            "delete",
            async token =>
            {
                await _clientProvider.Client.DeleteObjectAsync(
                    new DeleteObjectRequest { BucketName = _options.BucketName, Key = key },
                    token);
                return true;
            },
            cancellationToken);

        // Eliminar um objeto inexistente é sucesso: a operação é idempotente.
        return result.Status is VideoStorageStatus.Success or VideoStorageStatus.NotFound
            ? new VideoObjectDeletionOutcome(VideoStorageStatus.Success)
            : new VideoObjectDeletionOutcome(result.Status, result.FailureCode);
    }

    private bool TryResolveKey(string objectKey, Guid? ownerTrainerId, out string key)
    {
        if (!ExerciseVideo.IsObjectKeyOwnedBy(objectKey, ownerTrainerId))
        {
            _logger.LogWarning(
                VideoLogEvents.StorageObjectKeyRefused,
                "R2 operation refused for an identifier outside the owner space.");

            key = string.Empty;
            return false;
        }

        key = $"{_options.KeyPrefix}/{objectKey}";
        return true;
    }

    private bool IsPresignLifetimeValid(DateTime expiresAt)
    {
        var lifetime = expiresAt - _clock.UtcNow;
        return expiresAt.Kind == DateTimeKind.Utc &&
            lifetime >= TimeSpan.FromSeconds(1) &&
            lifetime <= MaxPresignLifetime;
    }

    /// <summary>A assinatura é cálculo local; uma falha aqui é configuração, não rede.</summary>
    private async Task<Uri?> PresignAsync(GetPreSignedUrlRequest request, string operation)
    {
        try
        {
            var url = await _clientProvider.Client.GetPreSignedURLAsync(request);
            return Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                uri.Scheme == Uri.UriSchemeHttps
                    ? uri
                    : null;
        }
        catch (AmazonClientException exception)
        {
            _logger.LogError(
                VideoLogEvents.StorageFailure,
                "R2 {Operation} failed with failure type {FailureType}.",
                operation,
                exception.GetType().Name);
            return null;
        }
    }

    private async Task<SendResult<T>> SendAsync<T>(
        string operation,
        Func<CancellationToken, Task<T>> send,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.Timeout);

        try
        {
            return new SendResult<T>(VideoStorageStatus.Success, await send(timeout.Token));
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return new SendResult<T>(
                VideoStorageStatus.NotFound, default, HttpStatus: exception.StatusCode);
        }
        catch (AmazonS3Exception exception)
        {
            var status = IsTransient(exception.StatusCode)
                ? VideoStorageStatus.TransientFailure
                : VideoStorageStatus.PermanentFailure;

            _logger.LogWarning(
                VideoLogEvents.StorageFailure,
                "R2 {Operation} failed with status {StatusCode}.",
                operation,
                (int)exception.StatusCode);

            return new SendResult<T>(
                status,
                default,
                $"r2_http_{(int)exception.StatusCode}",
                exception.StatusCode);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                VideoLogEvents.StorageFailure,
                "R2 {Operation} timed out.",
                operation);

            return new SendResult<T>(
                VideoStorageStatus.TransientFailure, default, "r2_timeout");
        }
        catch (Exception exception) when (exception is AmazonClientException or
            HttpRequestException or IOException)
        {
            _logger.LogWarning(
                VideoLogEvents.StorageFailure,
                "R2 {Operation} could not reach the provider with the failure type {FailureType}.",
                operation,
                exception.GetType().Name);

            return new SendResult<T>(
                VideoStorageStatus.TransientFailure, default, "r2_unreachable");
        }
    }

    private static bool IsTransient(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests ||
        (int)statusCode >= 500;

    private static string? NormalizeETag(string? eTag)
    {
        var normalized = eTag?.Trim().Trim('"');
        return string.IsNullOrEmpty(normalized) ||
            normalized.Length > MaxETagLength ||
            normalized.Any(character => character is < '!' or > '~' or '"')
                ? null
                : normalized;
    }

    /// <summary>Lê exatamente o número de bytes pedido; nunca aloca a partir do fornecedor.</summary>
    private static async Task<byte[]?> ReadExactlyAsync(
        Stream stream,
        int length,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[length];
        var read = 0;
        while (read < length)
        {
            var count = await stream.ReadAsync(buffer.AsMemory(read, length - read), cancellationToken);
            if (count == 0)
                return null;
            read += count;
        }

        return buffer;
    }

    private readonly record struct SendResult<T>(
        VideoStorageStatus Status,
        T? Value,
        string? FailureCode = null,
        HttpStatusCode? HttpStatus = null);
}
