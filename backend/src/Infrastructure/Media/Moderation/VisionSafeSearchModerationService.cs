using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Application.Common.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Media.Moderation;

/// <summary>
/// Implementa IImageModerationService sobre Google Cloud Vision
/// SafeSearch, de forma síncrona e fail-closed.
/// </summary>
internal sealed class VisionSafeSearchModerationService : IImageModerationService
{
    private const string AnnotatePath = "v1/images:annotate";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false
    };

    private readonly HttpClient _httpClient;
    private readonly VisionOptions _options;
    private readonly IVisionAccessTokenProvider _tokenProvider;
    private readonly ILogger<VisionSafeSearchModerationService> _logger;

    public VisionSafeSearchModerationService(
        HttpClient httpClient,
        IOptions<VisionOptions> options,
        IVisionAccessTokenProvider tokenProvider,
        ILogger<VisionSafeSearchModerationService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _tokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ImageModerationResult> ReviewAsync(
        ImageModerationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_options.Enabled)
            return Unavailable("vision_disabled");

        if (request.Content.IsEmpty)
            return Unavailable("vision_empty_content");

        var token = await _tokenProvider.GetAccessTokenAsync(cancellationToken);
        if (token is null)
            return Unavailable("vision_token_unavailable");

        var body = new VisionAnnotateRequest(
        [
            new VisionImageRequest(
                new VisionImage(Convert.ToBase64String(request.Content.Span)),
                [new VisionFeature("SAFE_SEARCH_DETECTION")])
        ]);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, AnnotatePath)
        {
            Content = JsonContent.Create(body, options: JsonOptions)
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            // O timeout do HttpClient tem de incluir o corpo, não apenas os headers.
            using var response = await _httpClient.SendAsync(
                httpRequest, HttpCompletionOption.ResponseContentRead, cancellationToken);

            var payload = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                return FromErrorResponse(response.StatusCode, payload);

            return FromAnnotation(payload);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Unavailable("vision_timeout");
        }
        catch (HttpRequestException)
        {
            return Unavailable("vision_unreachable");
        }
    }

    private ImageModerationResult FromErrorResponse(HttpStatusCode statusCode, string payload)
    {
        if (statusCode == HttpStatusCode.TooManyRequests ||
            string.Equals(Deserialize<VisionErrorEnvelope>(payload)?.Error?.Status,
            "RESOURCE_EXHAUSTED", StringComparison.Ordinal))
        {
            _logger.LogWarning(
                MediaLogEvents.ModerationQuotaExhausted,
                "Vision moderation quota is exhausted; avatar uploads are blocked until it recovers.");

            return new ImageModerationResult(
                ImageModerationVerdict.Unavailable, "vision_quota_exhausted");
        }

        return Unavailable($"vision_http_{(int)statusCode}");
    }

    private ImageModerationResult FromAnnotation(string payload)
    {
        var responses = Deserialize<VisionAnnotateResponse>(payload)?.Responses;
        if (responses is not { Count: 1 })
            return Unavailable("vision_invalid_response");

        var image = responses[0];
        if (image is null)
            return Unavailable("vision_invalid_response");
        if (image.Error is not null)
            return string.Equals(image.Error.Status, "RESOURCE_EXHAUSTED", StringComparison.Ordinal)
                ? FromErrorResponse(HttpStatusCode.TooManyRequests, string.Empty)
                : Unavailable("vision_image_error");

        var annotation = image.SafeSearchAnnotation;
        if (annotation is null)
            return Unavailable("vision_invalid_annotation");

        var result = SafeSearchPolicy.Evaluate(
            annotation.Adult, annotation.Violence, annotation.Racy);

        if (result.Verdict == ImageModerationVerdict.Unavailable)
            return Unavailable(result.ReasonCode ?? "vision_unknown_likelihood");

        if (result.Verdict != ImageModerationVerdict.Approved)
        {
            // A categoria fica só no log interno; ao cliente chega um código
            // único que não distingue rejeição de revisão.
            _logger.LogInformation(
                MediaLogEvents.ModerationNotApproved,
                "Vision moderation returned {Verdict} for category {Category}.",
                result.Verdict, result.ReasonCode);
        }

        return result;
    }

    private ImageModerationResult Unavailable(string reasonCode)
    {
        _logger.LogWarning(
            MediaLogEvents.ModerationUnavailable,
            "Vision moderation is unavailable with reason: {ReasonCode}.", reasonCode);

        return new ImageModerationResult(
            ImageModerationVerdict.Unavailable, reasonCode);
    }

    private static T? Deserialize<T>(string payload) where T : class
    {
        if (string.IsNullOrEmpty(payload))
            return null;

        try
        {
            return JsonSerializer.Deserialize<T>(payload, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
