using System.Net;
using Application.Common.Abstractions;
using Infrastructure.IntegrationTests.Support;
using Infrastructure.Media.Cloudinary;
using Infrastructure.Media.Moderation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.IntegrationTests.Media;

/// <summary>
/// Prova que os adapters de media nunca registam segredo, assinatura, token,
/// conteúdo da imagem ou corpo de resposta do fornecedor — nem sequer nos
/// caminhos de falha, que são os que mais tentam "ajudar" com detalhe.
/// </summary>
public sealed class MediaLogSanitizationTests
{
    private const string Secret = "sentinel-api-secret-must-not-be-logged";
    private const string AccessToken = "sentinel-access-token-must-not-be-logged";
    private const string ProviderEcho = "sentinel-provider-body-must-not-be-logged";
    private static readonly byte[] Content = [0xDE, 0xAD, 0xBE, 0xEF, 0x42];
    private static readonly Guid TrainerId = Guid.NewGuid();

    [Fact]
    public async Task Cloudinary_FailurePathsLogOnlyCategoryAndStatus()
    {
        var logger = new ListLogger<CloudinaryMediaStorage>();
        var stub = new MediaHttpStub()
            .Respond(HttpStatusCode.BadRequest, $$$"""{"error":{"message":"{{{ProviderEcho}}}"}}""")
            .Respond(HttpStatusCode.OK, $$$"""{"public_id":"elsewhere/{{{ProviderEcho}}}","secure_url":"https://evil.example"}""")
            .Throw(new HttpRequestException(ProviderEcho));
        var storage = new CloudinaryMediaStorage(
            stub.CreateClient("https://api.cloudinary.com/"),
            Options.Create(new CloudinaryOptions
            {
                Enabled = true,
                CloudName = "demo",
                ApiKey = "1",
                ApiSecret = Secret,
                FolderRoot = "pt-manager"
            }),
            new TestClock(DateTime.UtcNow),
            logger);
        var request = new MediaUploadRequest(Content, "image/webp", MediaAssetKind.ClientAvatar, TrainerId);

        await storage.UploadAsync(request, TestContext.Current.CancellationToken);
        await storage.UploadAsync(request, TestContext.Current.CancellationToken);
        await storage.UploadAsync(request, TestContext.Current.CancellationToken);
        await storage.DeleteAsync($"other/{ProviderEcho}", TrainerId, TestContext.Current.CancellationToken);

        Assert.NotEmpty(logger.Messages);
        AssertClean(logger.Messages, Secret, "signature=", ProviderEcho, Convert.ToBase64String(Content));
    }

    [Fact]
    public async Task Vision_FailurePathsLogOnlyClosedReasonCodes()
    {
        var logger = new ListLogger<VisionSafeSearchModerationService>();
        var stub = new MediaHttpStub()
            .Respond(HttpStatusCode.InternalServerError, $$$"""{"error":{"message":"{{{ProviderEcho}}}"}}""")
            .Respond(HttpStatusCode.TooManyRequests, $$$"""{"error":{"status":"RESOURCE_EXHAUSTED","message":"{{{ProviderEcho}}}"}}""")
            .Respond(HttpStatusCode.OK, """{"responses":[{"safeSearchAnnotation":{"adult":"VERY_LIKELY","violence":"UNLIKELY","racy":"UNLIKELY"}}]}""");
        var service = new VisionSafeSearchModerationService(
            stub.CreateClient("https://vision.googleapis.com/"),
            Options.Create(new VisionOptions { Enabled = true }),
            new FixedToken(),
            logger);
        var request = new ImageModerationRequest(Content, "image/webp");

        await service.ReviewAsync(request, TestContext.Current.CancellationToken);
        await service.ReviewAsync(request, TestContext.Current.CancellationToken);
        await service.ReviewAsync(request, TestContext.Current.CancellationToken);

        Assert.Contains(logger.Messages, message => message.Contains("quota", StringComparison.OrdinalIgnoreCase));
        AssertClean(logger.Messages, AccessToken, ProviderEcho, Convert.ToBase64String(Content));
    }

    private static void AssertClean(IReadOnlyList<string> messages, params string[] forbidden)
    {
        foreach (var message in messages)
        {
            foreach (var value in forbidden)
                Assert.DoesNotContain(value, message, StringComparison.Ordinal);
        }
    }

    private sealed class FixedToken : IVisionAccessTokenProvider
    {
        public Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken) =>
            Task.FromResult<string?>(AccessToken);
    }

    private sealed class ListLogger<T> : ILogger<T>
    {
        private readonly List<string> _messages = [];

        public IReadOnlyList<string> Messages => _messages;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            // Regista a mensagem formatada, os valores estruturados e a
            // excepção: é aqui que um detalhe sensível se esconderia.
            var structured = state is IEnumerable<KeyValuePair<string, object?>> pairs
                ? string.Join(';', pairs.Select(pair => $"{pair.Key}={pair.Value}"))
                : string.Empty;
            _messages.Add($"{formatter(state, exception)}|{structured}|{exception}");
        }
    }
}
