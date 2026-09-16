using System.Net;
using System.Net.Http.Headers;
using System.Web;
using Amazon.Runtime;
using Application.Common.Abstractions;
using Application.Features.Training.ExerciseVideos.Abstractions;
using Domain.Entities.Training;
using Infrastructure.Media.Video;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.IntegrationTests.Media.Video;

/// <summary>
/// Prova o adapter R2 sobre o AWS SDK real com o transporte HTTP substituído:
/// identificadores fora do owner nunca geram I/O, a assinatura restringe o upload,
/// as falhas são classificadas e os logs não expõem identificadores nem segredos.
/// </summary>
public sealed class R2VideoObjectStorageTests
{
    private const string Secret = "r2-secret-access-key-double";
    private static readonly Guid TrainerId = Guid.NewGuid();
    private static readonly DateTime Now = DateTime.UtcNow;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task EveryOperation_WhenDisabled_ReturnsDisabledWithoutHttp()
    {
        var (storage, http, _) = Create(enabled: false);
        var key = Key(TrainerId);

        Assert.Equal(VideoStorageStatus.Disabled, (await storage.CreateUploadAuthorizationAsync(
            key, TrainerId, "video/mp4", 10, Now.AddMinutes(15), Token)).Status);
        Assert.Equal(VideoStorageStatus.Disabled, (await storage.GetObjectInfoAsync(key, TrainerId, Token)).Status);
        Assert.Equal(VideoStorageStatus.Disabled, (await storage.CreatePlaybackUrlAsync(
            key, TrainerId, Now.AddMinutes(30), Token)).Status);
        Assert.Equal(VideoStorageStatus.Disabled, (await storage.DeleteAsync(key, TrainerId, Token)).Status);
        Assert.Equal(VideoRangeReadStatus.Disabled, (await storage.ReadRangeAsync(
            key, TrainerId, 0, 10, "etag", Token)).Status);
        Assert.Empty(http.Requests);
    }

    [Fact]
    public async Task EveryOperation_RefusesIdentifiersOutsideTheOwnerSpaceWithoutHttp()
    {
        var (storage, http, _) = Create();
        var foreign = Key(Guid.NewGuid());

        Assert.Equal("r2_object_key_not_owned", (await storage.CreateUploadAuthorizationAsync(
            foreign, TrainerId, "video/mp4", 10, Now.AddMinutes(15), Token)).FailureCode);
        Assert.Equal("r2_object_key_not_owned", (await storage.GetObjectInfoAsync(foreign, TrainerId, Token)).FailureCode);
        Assert.Equal("r2_object_key_not_owned", (await storage.DeleteAsync(Key(null), TrainerId, Token)).FailureCode);
        Assert.Equal("r2_object_key_not_owned", (await storage.CreatePlaybackUrlAsync(
            "exercise-videos/global/../trainers/x", null, Now.AddMinutes(30), Token)).FailureCode);
        Assert.Empty(http.Requests);
    }

    [Fact]
    public async Task UploadAuthorization_IsASignedPutBoundToContentTypeAndLength()
    {
        var (storage, http, _) = Create();
        var key = Key(TrainerId);

        var outcome = await storage.CreateUploadAuthorizationAsync(
            key, TrainerId, "video/mp4", 52_428_800, Now.AddMinutes(15), Token);

        Assert.Equal(VideoStorageStatus.Success, outcome.Status);
        var url = outcome.Authorization!.Url;
        var query = HttpUtility.ParseQueryString(url.Query);
        Assert.Equal(Uri.UriSchemeHttps, url.Scheme);
        Assert.EndsWith($"/videos-bucket/pt-manager/test/{key}", url.AbsolutePath, StringComparison.Ordinal);
        Assert.InRange(int.Parse(query["X-Amz-Expires"]!, System.Globalization.CultureInfo.InvariantCulture), 880, 900);
        Assert.Contains("content-type", query["X-Amz-SignedHeaders"], StringComparison.Ordinal);
        Assert.Contains("content-length", query["X-Amz-SignedHeaders"], StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, url.AbsoluteUri, StringComparison.Ordinal);
        Assert.Equal(("PUT", "video/mp4", 52_428_800L), (outcome.Authorization.Method, outcome.Authorization.ContentType, outcome.Authorization.ContentLength));
        Assert.Empty(http.Requests);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8 * 24 * 60)]
    public async Task Presign_RefusesLifetimesOutsideTheProviderRange(int minutes)
    {
        var (storage, http, _) = Create();

        var outcome = await storage.CreatePlaybackUrlAsync(Key(TrainerId), TrainerId, Now.AddMinutes(minutes), Token);

        Assert.Equal("r2_presign_request_invalid", outcome.FailureCode);
        Assert.Empty(http.Requests);
    }

    [Fact]
    public async Task PlaybackUrl_IsASignedGetWithTheRequestedLifetime()
    {
        var (storage, _, _) = Create();

        var outcome = await storage.CreatePlaybackUrlAsync(Key(null), null, Now.AddMinutes(30), Token);

        var query = HttpUtility.ParseQueryString(outcome.Playback!.Url.Query);
        Assert.InRange(int.Parse(query["X-Amz-Expires"]!, System.Globalization.CultureInfo.InvariantCulture), 1780, 1800);
        Assert.Equal("host", query["X-Amz-SignedHeaders"]);
    }

    [Fact]
    public async Task ObjectInfo_ReturnsTheProviderSizeETagAndType()
    {
        var (storage, http, _) = Create();
        http.Respond(() =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([]) };
            response.Content.Headers.ContentLength = 52_428_800;
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");
            response.Headers.ETag = new EntityTagHeaderValue("\"9b2cf535f27731c974343645a3985328\"");
            return response;
        });

        var outcome = await storage.GetObjectInfoAsync(Key(TrainerId), TrainerId, Token);

        Assert.Equal(
            new VideoObjectInfo(52_428_800, "9b2cf535f27731c974343645a3985328", "video/mp4"),
            outcome.Info);
        Assert.Equal(HttpMethod.Head, Assert.Single(http.Requests).Method);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, VideoStorageStatus.NotFound)]
    [InlineData(HttpStatusCode.ServiceUnavailable, VideoStorageStatus.TransientFailure)]
    [InlineData(HttpStatusCode.TooManyRequests, VideoStorageStatus.TransientFailure)]
    [InlineData(HttpStatusCode.Forbidden, VideoStorageStatus.PermanentFailure)]
    public async Task ObjectInfo_ClassifiesProviderStatuses(HttpStatusCode statusCode, VideoStorageStatus expected)
    {
        var (storage, http, _) = Create();
        http.Respond(() => new HttpResponseMessage(statusCode) { Content = new ByteArrayContent([]) });

        var outcome = await storage.GetObjectInfoAsync(Key(TrainerId), TrainerId, Token);

        Assert.Equal(expected, outcome.Status);
    }

    [Fact]
    public async Task ObjectInfo_WhenNetworkFails_IsTransient()
    {
        var (storage, http, _) = Create();
        http.Respond(() => throw new HttpRequestException("connection reset"));

        var outcome = await storage.GetObjectInfoAsync(Key(TrainerId), TrainerId, Token);

        Assert.Equal(VideoStorageStatus.TransientFailure, outcome.Status);
    }

    [Fact]
    public async Task RangeRead_SendsTheRangeAndTheETagPrecondition()
    {
        var (storage, http, _) = Create();
        http.Respond(() => new HttpResponseMessage(HttpStatusCode.PartialContent)
        {
            Content = new ByteArrayContent([1, 2, 3, 4])
        });

        var outcome = await storage.ReadRangeAsync(Key(TrainerId), TrainerId, 100, 4, "etag-1", Token);

        Assert.Equal([1, 2, 3, 4], outcome.Data);
        var request = Assert.Single(http.Requests);
        Assert.Equal("bytes=100-103", request.Headers.Range!.ToString());
        Assert.Equal("\"etag-1\"", Assert.Single(request.Headers.IfMatch).Tag);
    }

    [Fact]
    public async Task RangeRead_WhenETagNoLongerMatches_ReportsChanged()
    {
        var (storage, http, _) = Create();
        http.Respond(() => new HttpResponseMessage(HttpStatusCode.PreconditionFailed) { Content = new ByteArrayContent([]) });

        var outcome = await storage.ReadRangeAsync(Key(TrainerId), TrainerId, 0, 16, "etag-1", Token);

        Assert.Equal(VideoRangeReadStatus.Changed, outcome.Status);
    }

    [Fact]
    public async Task RangeRead_WhenProviderReturnsFewerBytes_NeverReturnsPartialData()
    {
        var (storage, http, _) = Create();
        http.Respond(() => new HttpResponseMessage(HttpStatusCode.PartialContent) { Content = new ByteArrayContent([1, 2]) });

        var outcome = await storage.ReadRangeAsync(Key(TrainerId), TrainerId, 0, 4, "etag-1", Token);

        Assert.Equal((VideoRangeReadStatus.PermanentFailure, (byte[]?)null), (outcome.Status, outcome.Data));
    }

    [Theory]
    [InlineData(HttpStatusCode.NoContent)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task Delete_IsIdempotent(HttpStatusCode statusCode)
    {
        var (storage, http, _) = Create();
        http.Respond(() => new HttpResponseMessage(statusCode) { Content = new ByteArrayContent([]) });

        var outcome = await storage.DeleteAsync(Key(TrainerId), TrainerId, Token);

        Assert.Equal(VideoStorageStatus.Success, outcome.Status);
        Assert.Equal(HttpMethod.Delete, Assert.Single(http.Requests).Method);
    }

    [Fact]
    public async Task FailureLogs_NeverContainIdentifiersUrlsOrSecrets()
    {
        var (storage, http, logs) = Create();
        var key = Key(TrainerId);
        http.Respond(() => new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent("<Error><Code>AccessDenied</Code><Key>" + key + "</Key></Error>")
        });

        await storage.GetObjectInfoAsync(key, TrainerId, Token);
        await storage.DeleteAsync(Key(Guid.NewGuid()), TrainerId, Token);

        Assert.NotEmpty(logs.Messages);
        Assert.All(logs.Messages, message =>
        {
            Assert.DoesNotContain(key, message, StringComparison.Ordinal);
            Assert.DoesNotContain(TrainerId.ToString("N"), message, StringComparison.Ordinal);
            Assert.DoesNotContain(Secret, message, StringComparison.Ordinal);
            Assert.DoesNotContain("AccessDenied", message, StringComparison.Ordinal);
            Assert.DoesNotContain("X-Amz-Signature", message, StringComparison.Ordinal);
        });
    }

    private static string Key(Guid? owner) => ExerciseVideo.BuildObjectKey(owner, Guid.NewGuid());

    private static (R2VideoObjectStorage Storage, ScriptedS3Handler Http, RecordingLogger Logs) Create(bool enabled = true)
    {
        var options = Options.Create(new R2Options
        {
            Enabled = enabled,
            AccountId = "0123456789abcdef0123456789abcdef",
            AccessKeyId = "r2-access-key-double",
            SecretAccessKey = Secret,
            BucketName = "videos-bucket",
            KeyPrefix = "pt-manager/test",
            Timeout = TimeSpan.FromSeconds(5)
        });

        var http = new ScriptedS3Handler();
        var logs = new RecordingLogger();
        var storage = new R2VideoObjectStorage(
            new R2ClientProvider(options, new StubHttpClientFactory(http)),
            options,
            new SystemClockDouble(),
            logs);

        return (storage, http, logs);
    }

    private sealed class SystemClockDouble : IClock
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : HttpClientFactory
    {
        public override HttpClient CreateHttpClient(IClientConfig clientConfig) => new(handler, disposeHandler: false);
        public override bool UseSDKHttpClientCaching(IClientConfig clientConfig) => false;
        public override bool DisposeHttpClientsAfterUse(IClientConfig clientConfig) => true;
    }

    private sealed class ScriptedS3Handler : HttpMessageHandler
    {
        private Func<HttpResponseMessage> _response =
            () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([]) };

        public List<HttpRequestMessage> Requests { get; } = [];

        public void Respond(Func<HttpResponseMessage> response) => _response = response;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            var response = _response();
            response.RequestMessage = request;
            return Task.FromResult(response);
        }
    }

    private sealed class RecordingLogger : ILogger<R2VideoObjectStorage>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception) + (exception is null ? string.Empty : exception.ToString()));
        }
    }
}
