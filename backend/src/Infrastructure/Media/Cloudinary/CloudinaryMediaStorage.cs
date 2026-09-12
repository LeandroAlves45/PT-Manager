using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application.Common.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Media.Cloudinary;

/// <summary>
/// Implementa IMediaStorage sobre a Upload API do Cloudinary, com HttpClient tipado
/// e sem SDK.
/// </summary>
internal sealed class CloudinaryMediaStorage : IMediaStorage
{
    private const string DeliveryHost = "res.cloudinary.com";
    private const int MaxReferenceLength = 500;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false
    };

    private readonly HttpClient _httpClient;
    private readonly CloudinaryOptions _options;
    private readonly IClock _clock;
    private readonly ILogger<CloudinaryMediaStorage> _logger;

    public CloudinaryMediaStorage(
        HttpClient httpClient,
        IOptions<CloudinaryOptions> options,
        IClock clock,
        ILogger<CloudinaryMediaStorage> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<MediaUploadOutcome> UploadAsync(
        MediaUploadRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_options.Enabled)
            return new MediaUploadOutcome(MediaStorageStatus.Disabled);

        var folder = CloudinaryFolderNaming.FolderFor(
            _options.FolderRoot,
            request.Kind,
            request.TrainerId);
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["folder"] = folder,
            ["overwrite"] = "false",
            ["timestamp"] = UnixTimestamp(),
            ["unique_filename"] = "true",
            ["use_filename"] = "false"
        };

        using var content = new MultipartFormDataContent();
        AddSignedFields(content, parameters);

        var file = new ByteArrayContent(request.Content.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue(request.ContentType);

        // O nome é ignorado pelo fornecedor com use_filename=false; é fixo para
        // que nenhum nome escolhido pelo utilizador atravesse a fronteira.
        content.Add(file, "file", "upload");

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"v1_1/{_options.CloudName}/image/upload")
        {
            Content = content
        };

        var response = await SendAsync(httpRequest, "upload", cancellationToken);
        if (response.Status != MediaStorageStatus.Success)
            return new MediaUploadOutcome(response.Status, FailureCode: response.FailureCode);

        var payload = Deserialize<UploadResponse>(response.Body);
        if (payload is null || !IsValidUpload(payload, folder))
        {
            _logger.LogWarning(
                MediaLogEvents.StorageInvalidResponse,
                "Cloudinary upload response did not satisfy the storage contracts.");

            return new MediaUploadOutcome(
                MediaStorageStatus.InvalidResponse,
                FailureCode: "cloudinary_invalid_response");
        }

        return new MediaUploadOutcome(
            MediaStorageStatus.Success,
            new StoredMedia(
                payload.SecureUrl!,
                payload.PublicId!));
    }

    public async Task<MediaDeletionOutcome> DeleteAsync(
        string publicId,
        Guid trainerId,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
            return new MediaDeletionOutcome(MediaStorageStatus.Disabled);

        if (string.IsNullOrWhiteSpace(publicId) || publicId.Length > MaxReferenceLength ||
            !CloudinaryFolderNaming.IsOwnedBy(_options.FolderRoot, trainerId, publicId))
        {
            _logger.LogWarning(
                MediaLogEvents.StorageDeletionRefused,
                "Cloudinary deletion refused for an identifier outside the tenant folder.");

            return new MediaDeletionOutcome(
                MediaStorageStatus.PermanentFailure,
                "cloudinary_public_id_not_owned");
        }

        var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["invalidate"] = "true",
            ["public_id"] = publicId,
            ["timestamp"] = UnixTimestamp()
        };

        using var content = new MultipartFormDataContent();
        AddSignedFields(content, parameters);

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"v1_1/{_options.CloudName}/image/destroy")
        {
            Content = content
        };

        var response = await SendAsync(httpRequest, "destroy", cancellationToken);
        if (response.Status != MediaStorageStatus.Success)
            return new MediaDeletionOutcome(response.Status, FailureCode: response.FailureCode);

        var payload = Deserialize<DestroyResponse>(response.Body);

        // "not found" é sucesso: a eliminação é entregue at-least-once, e uma
        // segunda entrega depois de uma primeira bem sucedida é esperada.
        return payload?.Result is "ok" or "not found"
            ? new MediaDeletionOutcome(MediaStorageStatus.Success)
            : new MediaDeletionOutcome(
                MediaStorageStatus.InvalidResponse,
                "cloudinary_invalid_response");
    }

    private void AddSignedFields(
        MultipartFormDataContent content,
        Dictionary<string, string> parameters)
    {
        var signature = CloudinarySignature.Sign(parameters, _options.ApiSecret!);

        foreach (var parameter in parameters)
            content.Add(new StringContent(parameter.Value), parameter.Key);

        content.Add(new StringContent(_options.ApiKey!), "api_key");
        content.Add(new StringContent(signature), "signature");
    }

    private async Task<ProviderResponse> SendAsync(
        HttpRequestMessage request,
        string operation,
        CancellationToken cancellationToken)
    {
        try
        {
            // O timeout do HttpClient tem de incluir o corpo, não apenas os headers.
            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseContentRead,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                return new ProviderResponse(
                    MediaStorageStatus.Success,
                    null,
                    body);
            }

            var statusCode = (int)response.StatusCode;
            var status = IsTransient(response.StatusCode)
                ? MediaStorageStatus.TransientFailure
                : MediaStorageStatus.PermanentFailure;

            // Só categoria e status: o corpo de erro do fornecedor pode ecoar
            // parâmetros do pedido e não entra em logs.
            _logger.LogWarning(
                MediaLogEvents.StorageFailure,
                "Cloudinary {Operation} failed with status code {StatusCode}.",
                operation,
                statusCode);

            return new ProviderResponse(status, $"cloudinary_http_{statusCode}", null);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                MediaLogEvents.StorageFailure,
                "Cloudinary {Operation} timed out.",
                operation);

            return new ProviderResponse(
                MediaStorageStatus.TransientFailure,
                "cloudinary_timeout",
                null);
        }
        catch (HttpRequestException)
        {
            _logger.LogWarning(
                MediaLogEvents.StorageFailure,
                "Cloudinary {Operation} was unreachable.",
                operation);

            return new ProviderResponse(
                MediaStorageStatus.TransientFailure,
                "cloudinary_unreachable",
                null);
        }
    }

    private static bool IsValidUpload(UploadResponse payload, string folder)
    {
        if (string.IsNullOrWhiteSpace(payload.PublicId) ||
            payload.PublicId.Length > MaxReferenceLength ||
            !payload.PublicId.StartsWith(folder + "/", StringComparison.Ordinal))
            return false;

        return !string.IsNullOrWhiteSpace(payload.SecureUrl) &&
            payload.SecureUrl.Length <= MaxReferenceLength &&
            Uri.TryCreate(payload.SecureUrl, UriKind.Absolute, out var url) &&
            url.Scheme == Uri.UriSchemeHttps &&
            string.Equals(url.Host, DeliveryHost, StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrEmpty(url.UserInfo);
    }

    private static T? Deserialize<T>(string? body) where T : class
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;

        try
        {
            return JsonSerializer.Deserialize<T>(body, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool IsTransient(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests ||
        (int)statusCode == 420 ||
        (int)statusCode >= 500;

    private string UnixTimestamp() =>
        new DateTimeOffset(DateTime.SpecifyKind(_clock.UtcNow, DateTimeKind.Utc))
            .ToUnixTimeSeconds()
            .ToString(CultureInfo.InvariantCulture);

    private sealed record ProviderResponse(
        MediaStorageStatus Status, string? FailureCode, string? Body);

    private sealed record UploadResponse(
        [property: JsonPropertyName("public_id")] string? PublicId,
        [property: JsonPropertyName("secure_url")] string? SecureUrl);

    private sealed record DestroyResponse(
        [property: JsonPropertyName("result")] string? Result);
}
