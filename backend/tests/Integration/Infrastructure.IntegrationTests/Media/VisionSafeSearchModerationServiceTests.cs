using System.Net;
using Application.Common.Abstractions;
using Infrastructure.Media.Moderation;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Infrastructure.IntegrationTests.Media;

/// <summary>
/// Prova o comportamento fail-closed da moderação e a política de limiares.
/// </summary>
public sealed class VisionSafeSearchModerationServiceTests
{
    [Theory]
    [InlineData("VERY_UNLIKELY", "VERY_UNLIKELY", "VERY_UNLIKELY", ImageModerationVerdict.Approved)]
    [InlineData("UNLIKELY", "UNLIKELY", "LIKELY", ImageModerationVerdict.Approved)]
    [InlineData("LIKELY", "UNLIKELY", "UNLIKELY", ImageModerationVerdict.Rejected)]
    [InlineData("VERY_LIKELY", "UNLIKELY", "UNLIKELY", ImageModerationVerdict.Rejected)]
    [InlineData("UNLIKELY", "LIKELY", "UNLIKELY", ImageModerationVerdict.Rejected)]
    [InlineData("UNLIKELY", "UNLIKELY", "VERY_LIKELY", ImageModerationVerdict.Rejected)]
    [InlineData("POSSIBLE", "UNLIKELY", "UNLIKELY", ImageModerationVerdict.ReviewRequired)]
    [InlineData("UNKNOWN", "UNLIKELY", "UNLIKELY", ImageModerationVerdict.Unavailable)]
    [InlineData("UNLIKELY", null, "UNLIKELY", ImageModerationVerdict.Unavailable)]
    public void Policy_AppliesTheApprovedThresholds(
        string? adult, string? violence, string? racy, ImageModerationVerdict expected)
    {
        Assert.Equal(expected, SafeSearchPolicy.Evaluate(adult, violence, racy).Verdict);
    }

    /// <summary>
    /// Fotografia de fitness legítima pontua frequentemente LIKELY em racy. Se
    /// este teste falhar, o limiar foi baixado e o produto vai rejeitar o seu
    /// próprio público.
    /// </summary>
    [Fact]
    public void Policy_DoesNotRejectLikelyRacyFitnessPhotography()
    {
        Assert.Equal(
            ImageModerationVerdict.Approved,
            SafeSearchPolicy.Evaluate("UNLIKELY", "VERY_UNLIKELY", "LIKELY").Verdict);
    }

    [Fact]
    public async Task Review_WhenApproved_SendsBearerTokenAndSafeSearchFeature()
    {
        var stub = new MediaHttpStub().Respond(HttpStatusCode.OK, Annotation("VERY_UNLIKELY", "VERY_UNLIKELY", "UNLIKELY"));

        var result = await CreateService(stub).ReviewAsync(Request(), TestContext.Current.CancellationToken);

        Assert.Equal(ImageModerationVerdict.Approved, result.Verdict);
        var request = Assert.Single(stub.Requests);
        Assert.Equal("/v1/images:annotate", request.Uri.AbsolutePath);
        Assert.Equal("Bearer test-token", request.Authorization);
        Assert.Contains("SAFE_SEARCH_DETECTION", request.Body, StringComparison.Ordinal);
        Assert.Contains(Convert.ToBase64String([1, 2, 3]), request.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Review_WhenResponseBodyExceedsTimeout_IsUnavailable()
    {
        var stub = new MediaHttpStub().Respond(
            new DelayedMediaContent(Annotation("UNLIKELY", "UNLIKELY", "UNLIKELY")));
        using var client = stub.CreateClient("https://vision.googleapis.com/");
        client.Timeout = TimeSpan.FromMilliseconds(100);

        var result = await CreateService(stub, client: client).ReviewAsync(
            Request(), TestContext.Current.CancellationToken);

        Assert.Equal(ImageModerationVerdict.Unavailable, result.Verdict);
        Assert.Equal("vision_timeout", result.ReasonCode);
    }

    [Fact]
    public async Task Review_WhenDisabled_IsUnavailableWithoutIo()
    {
        var stub = new MediaHttpStub();

        var result = await CreateService(stub, enabled: false).ReviewAsync(Request(), TestContext.Current.CancellationToken);

        Assert.Equal(ImageModerationVerdict.Unavailable, result.Verdict);
        Assert.Empty(stub.Requests);
    }

    [Fact]
    public async Task Review_WhenTokenIsUnavailable_IsUnavailableWithoutIo()
    {
        var stub = new MediaHttpStub();

        var result = await CreateService(stub, token: null).ReviewAsync(Request(), TestContext.Current.CancellationToken);

        Assert.Equal(ImageModerationVerdict.Unavailable, result.Verdict);
        Assert.Empty(stub.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, """{"error":{"code":429,"status":"RESOURCE_EXHAUSTED"}}""", "vision_quota_exhausted")]
    [InlineData(HttpStatusCode.Forbidden, """{"error":{"code":403,"status":"RESOURCE_EXHAUSTED"}}""", "vision_quota_exhausted")]
    [InlineData(HttpStatusCode.InternalServerError, "{}", "vision_http_500")]
    [InlineData(HttpStatusCode.Unauthorized, "{}", "vision_http_401")]
    [InlineData(HttpStatusCode.OK, "not json", "vision_invalid_response")]
    [InlineData(HttpStatusCode.OK, """{"responses":[null]}""", "vision_invalid_response")]
    [InlineData(HttpStatusCode.OK, """{"responses":[]}""", "vision_invalid_response")]
    [InlineData(HttpStatusCode.OK, """{"responses":[{"error":{"code":3,"status":"INVALID_ARGUMENT"}}]}""", "vision_image_error")]
    public async Task Review_FailsClosedOnEveryUnexpectedPath(HttpStatusCode status, string body, string reason)
    {
        var stub = new MediaHttpStub().Respond(status, body);

        var result = await CreateService(stub).ReviewAsync(Request(), TestContext.Current.CancellationToken);

        Assert.Equal(ImageModerationVerdict.Unavailable, result.Verdict);
        Assert.Equal(reason, result.ReasonCode);
    }

    [Fact]
    public async Task Review_WhenNetworkFails_IsUnavailable()
    {
        var stub = new MediaHttpStub().Throw(new HttpRequestException("down"));

        var result = await CreateService(stub).ReviewAsync(Request(), TestContext.Current.CancellationToken);

        Assert.Equal(ImageModerationVerdict.Unavailable, result.Verdict);
    }

    [Fact]
    public async Task Review_WhenCallerCancels_PropagatesCancellation()
    {
        var stub = new MediaHttpStub().Respond(HttpStatusCode.OK, Annotation("UNLIKELY", "UNLIKELY", "UNLIKELY"));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => CreateService(stub).ReviewAsync(Request(), cancellation.Token));
    }

    private static VisionSafeSearchModerationService CreateService(
        MediaHttpStub stub,
        bool enabled = true,
        string? token = "test-token", HttpClient? client = null) =>
        new(
            client ?? stub.CreateClient("https://vision.googleapis.com/"),
            Options.Create(new VisionOptions { Enabled = enabled }),
            new FixedTokenProvider(token),
            NullLogger<VisionSafeSearchModerationService>.Instance);

    private static ImageModerationRequest Request() => new(new byte[] { 1, 2, 3 }, "image/webp");

    private static string Annotation(string adult, string violence, string racy) =>
        $$$"""{"responses":[{"safeSearchAnnotation":{"adult":"{{{adult}}}","spoof":"VERY_UNLIKELY","medical":"VERY_LIKELY","violence":"{{{violence}}}","racy":"{{{racy}}}"}}]}""";

    private sealed class FixedTokenProvider(string? token) : IVisionAccessTokenProvider
    {
        public Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken) =>
            Task.FromResult(token);
    }
}
