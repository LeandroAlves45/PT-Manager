using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Api.FunctionalTests.Support;
using Application.Common.Abstractions;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SkiaSharp;

namespace Api.FunctionalTests.Controllers;

/// <summary>
/// Prova a superfície HTTP das imagens geridas contra PostgreSQL real e o
/// processador de imagem real. Só a moderação e o storage — as duas dependências
/// externas pagas — são substituídos por duplos.
/// </summary>
/// <remarks>
/// Cobre os pontos 1, 2, 3 e 7 do Gate 5C na fronteira HTTP: recusas de
/// conteúdo, só o próprio cliente altera o seu avatar, veredictos não
/// aprovados nunca publicam, e isolamento de tenant.
/// </remarks>
[Collection(ApiTestCollection.Name)]
public sealed class MediaUploadControllerTests : IDisposable
{
    private const string LogoPath = "/api/v1/trainer-settings/logo";
    private const string AvatarPath = "/api/v1/portal/my-profile/avatar";

    private readonly ScriptedModeration _moderation = new();
    private readonly RecordingStorage _storage = new();
    private readonly ApiWebApplicationFactory _factory;
    private readonly ApiWebApplicationFactory _defaultFactory;

    public MediaUploadControllerTests(PostgresApiFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _defaultFactory = fixture.Factory;
        _factory = new ApiWebApplicationFactory(fixture.ConnectionString)
        {
            ConfigureServices = services =>
            {
                services.RemoveAll<IImageModerationService>();
                services.RemoveAll<IMediaStorage>();
                services.AddSingleton<IImageModerationService>(_moderation);
                services.AddSingleton<IMediaStorage>(_storage);
            }
        };
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _factory.Dispose();

    // ------------------------------------------------------------ PUT /logo

    [Fact]
    public async Task PutLogo_WithValidPng_PublishesReencodedWebpAndHidesThePublicId()
    {
        var trainer = await TrainerTenantSeeder.SeedTrainerAsync(_factory, $"logo-{Guid.NewGuid():N}", Token);

        var response = await TrainerClient(trainer.TrainerId)
            .PutAsync(LogoPath, Multipart(Png(800, 400), "image/png"), Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal(RecordingStorage.Url, body.GetProperty("logo_url").GetString());
        Assert.False(body.TryGetProperty("logo_public_id", out _));

        var uploaded = Assert.Single(_storage.Uploads);
        Assert.Equal("image/webp", uploaded.ContentType);
        Assert.Equal(trainer.TrainerId, uploaded.TrainerId);
        Assert.Equal(0, _moderation.Calls);
    }

    [Fact]
    public async Task PutLogo_WhenFileIsEmpty_ReturnsBadRequestWithoutExternalCalls()
    {
        var trainer = await TrainerTenantSeeder.SeedTrainerAsync(_factory, $"logo-{Guid.NewGuid():N}", Token);

        var response = await TrainerClient(trainer.TrainerId)
            .PutAsync(LogoPath, Multipart([], "image/png"), Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(_storage.Uploads);
    }

    [Fact]
    public async Task PutLogo_WhenFilePartIsMissing_ReturnsTheStableRequiredCode()
    {
        var trainer = await TrainerTenantSeeder.SeedTrainerAsync(_factory, $"logo-{Guid.NewGuid():N}", Token);
        using var content = new MultipartFormDataContent { { new StringContent("x"), "other" } };

        var response = await TrainerClient(trainer.TrainerId).PutAsync(LogoPath, content, Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("trainer_settings_logo_required", await response.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);
    }

    [Fact]
    public async Task PutLogo_WhenBytesDoNotMatchTheDeclaredType_ReturnsMismatch()
    {
        var trainer = await TrainerTenantSeeder.SeedTrainerAsync(_factory, $"logo-{Guid.NewGuid():N}", Token);

        var response = await TrainerClient(trainer.TrainerId)
            .PutAsync(LogoPath, Multipart(Png(200, 200), "image/jpeg"), Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("trainer_settings_logo_content_type_mismatch", await response.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);
        Assert.Empty(_storage.Uploads);
    }

    [Fact]
    public async Task PutLogo_WhenContentIsNotAnImage_ReturnsNotDecodable()
    {
        var trainer = await TrainerTenantSeeder.SeedTrainerAsync(_factory, $"logo-{Guid.NewGuid():N}", Token);
        var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\"><!ENTITY x SYSTEM \"file:///etc/passwd\"></svg>"u8.ToArray();

        var response = await TrainerClient(trainer.TrainerId)
            .PutAsync(LogoPath, Multipart(svg, "image/png"), Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("trainer_settings_logo_not_decodable", await response.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);
    }

    [Fact]
    public async Task PutLogo_WhenBodyIsNotMultipart_ReturnsUnsupportedMediaType()
    {
        var trainer = await TrainerTenantSeeder.SeedTrainerAsync(_factory, $"logo-{Guid.NewGuid():N}", Token);
        using var content = new ByteArrayContent(Png(200, 200));
        content.Headers.ContentType = new MediaTypeHeaderValue("image/png");

        var response = await TrainerClient(trainer.TrainerId).PutAsync(LogoPath, content, Token);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [Fact]
    public async Task PutLogo_WhenBodyExceedsTheRequestLimit_IsRejectedBeforeTheHandler()
    {
        var trainer = await TrainerTenantSeeder.SeedTrainerAsync(_factory, $"logo-{Guid.NewGuid():N}", Token);
        var oversized = new byte[7 * 1024 * 1024];

        var response = await TrainerClient(trainer.TrainerId)
            .PutAsync(LogoPath, Multipart(oversized, "image/png"), Token);

        // TestServer aplica o limite multipart no model binding, antes do handler.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(_storage.Uploads);
    }

    [Fact]
    public async Task PutLogo_WithClientRole_ReturnsForbidden()
    {
        var response = await _factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueClient(Guid.NewGuid(), Guid.NewGuid()))
            .PutAsync(LogoPath, Multipart(Png(200, 200), "image/png"), Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(_storage.Uploads);
    }

    [Fact]
    public async Task PutLogo_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _factory.CreateOriginClient()
            .PutAsync(LogoPath, Multipart(Png(200, 200), "image/png"), Token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Congela a decisão de não exigir Origin: a autenticação é por header
    /// Bearer, que um pedido cross-site não consegue anexar. Um cliente não
    /// browser, sem Origin, tem de continuar a funcionar.
    /// </summary>
    [Fact]
    public async Task PutLogo_WithoutOriginHeader_IsAcceptedBecauseAuthIsBearerOnly()
    {
        var trainer = await TrainerTenantSeeder.SeedTrainerAsync(_factory, $"logo-{Guid.NewGuid():N}", Token);
        // Evita o redirect HTTP para HTTPS, que remove o Bearer no cliente de teste.
        using var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

        var response = await client
            .WithBearer(TestJwtFactory.IssueTrainer(trainer.TrainerId))
            .PutAsync(LogoPath, Multipart(Png(200, 200), "image/png"), Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ------------------------------------------------------ PUT /my-avatar

    [Fact]
    public async Task PutAvatar_WithTrainerRole_ReturnsForbidden()
    {
        var trainer = await TrainerTenantSeeder.SeedTrainerAsync(_factory, $"avatar-{Guid.NewGuid():N}", Token);

        var response = await TrainerClient(trainer.TrainerId)
            .PutAsync(AvatarPath, Multipart(Png(300, 300), "image/png"), Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(_storage.Uploads);
    }

    [Fact]
    public async Task PutAvatar_WhenApproved_PersistsThePairForTheAuthenticatedClientOnly()
    {
        var (trainerId, clientUserId) = await PortalTestData.SeedActiveClientAsync(_factory, Token);
        var (_, neighbourUserId) = await PortalTestData.SeedActiveClientAsync(_factory, Token, trainerId);
        _moderation.Verdict = ImageModerationVerdict.Approved;

        var response = await ClientClient(clientUserId, trainerId)
            .PutAsync(AvatarPath, Multipart(Png(640, 480), "image/png"), Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(RecordingStorage.Url, (await ReadJsonAsync(response)).GetProperty("avatar_url").GetString());
        Assert.Equal(1, _moderation.Calls);
        Assert.Equal((RecordingStorage.Url, RecordingStorage.PublicId), await StoredAvatarAsync(clientUserId));
        Assert.Equal((null, null), await StoredAvatarAsync(neighbourUserId));
    }

    [Theory]
    [InlineData(ImageModerationVerdict.Rejected)]
    [InlineData(ImageModerationVerdict.ReviewRequired)]
    public async Task PutAvatar_WhenModerationDoesNotApprove_NeverPublishes(ImageModerationVerdict verdict)
    {
        var (trainerId, clientUserId) = await PortalTestData.SeedActiveClientAsync(_factory, Token);
        _moderation.Verdict = verdict;

        var response = await ClientClient(clientUserId, trainerId)
            .PutAsync(AvatarPath, Multipart(Png(300, 300), "image/png"), Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("portal_avatar_content_not_allowed", await response.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);
        Assert.Empty(_storage.Uploads);
        Assert.Equal((null, null), await StoredAvatarAsync(clientUserId));
    }

    [Fact]
    public async Task PutAvatar_WhenModerationIsUnavailable_FailsClosedWith503()
    {
        var (trainerId, clientUserId) = await PortalTestData.SeedActiveClientAsync(_factory, Token);
        _moderation.Verdict = ImageModerationVerdict.Unavailable;

        var response = await ClientClient(clientUserId, trainerId)
            .PutAsync(AvatarPath, Multipart(Png(300, 300), "image/png"), Token);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("portal_avatar_moderation_unavailable", await response.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);
        Assert.Empty(_storage.Uploads);
    }

    // --------------------------------------------------- DELETE /my-avatar

    [Fact]
    public async Task DeleteAvatar_ClearsThePairAndSchedulesDeletionThroughTheOutbox()
    {
        var (trainerId, clientUserId) = await PortalTestData.SeedActiveClientAsync(_factory, Token);
        _moderation.Verdict = ImageModerationVerdict.Approved;
        await ClientClient(clientUserId, trainerId)
            .PutAsync(AvatarPath, Multipart(Png(300, 300), "image/png"), Token);

        var response = await ClientClient(clientUserId, trainerId).DeleteAsync(AvatarPath, Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(JsonValueKind.Null, (await ReadJsonAsync(response)).GetProperty("avatar_url").ValueKind);
        Assert.Equal((null, null), await StoredAvatarAsync(clientUserId));
        Assert.Equal(1, await CountAvatarDeletionsAsync(trainerId));
    }

    // ------------------------------------------------------ kill-switches

    /// <summary>
    /// Com a configuração por omissão, Cloudinary e Vision estão desligados: o
    /// upload responde 503 sem exigir segredos, e a remoção continua disponível
    /// porque só escreve na base e na outbox.
    /// </summary>
    [Fact]
    public async Task DefaultConfiguration_BlocksUploadsButKeepsRemovalAvailable()
    {
        var trainer = await TrainerTenantSeeder.SeedTrainerAsync(_defaultFactory, $"kill-{Guid.NewGuid():N}", Token);
        var (trainerId, clientUserId) = await PortalTestData.SeedActiveClientAsync(_defaultFactory, Token);

        var logo = await _defaultFactory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueTrainer(trainer.TrainerId))
            .PutAsync(LogoPath, Multipart(Png(200, 200), "image/png"), Token);
        var avatar = await _defaultFactory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueClient(clientUserId, trainerId))
            .PutAsync(AvatarPath, Multipart(Png(200, 200), "image/png"), Token);
        var removal = await _defaultFactory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueClient(clientUserId, trainerId))
            .DeleteAsync(AvatarPath, Token);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, logo.StatusCode);
        Assert.Contains("trainer_settings_logo_storage_unavailable", await logo.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, avatar.StatusCode);
        Assert.Contains("portal_avatar_moderation_unavailable", await avatar.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, removal.StatusCode);
    }

    private HttpClient TrainerClient(Guid trainerId) =>
        _factory.CreateOriginClient().WithBearer(TestJwtFactory.IssueTrainer(trainerId));

    private HttpClient ClientClient(Guid clientUserId, Guid trainerId) =>
        _factory.CreateOriginClient().WithBearer(TestJwtFactory.IssueClient(clientUserId, trainerId));

    private async Task<(string? Url, string? PublicId)> StoredAvatarAsync(Guid clientUserId)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();
        var row = await context.Clients.IgnoreQueryFilters()
            .Where(client => client.UserId == clientUserId)
            .Select(client => new { client.AvatarUrl, client.AvatarPublicId })
            .SingleAsync(Token);
        return (row.AvatarUrl, row.AvatarPublicId);
    }

    private async Task<int> CountAvatarDeletionsAsync(Guid trainerId)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();
        return await context.OutboxMessages.IgnoreQueryFilters().CountAsync(
            message => message.TrainerId == trainerId && message.MessageType == "client-avatar.delete",
            Token);
    }

    private static MultipartFormDataContent Multipart(byte[] content, string contentType)
    {
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return new MultipartFormDataContent { { file, "file", "picture.png" } };
    }

    private static byte[] Png(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.MediumPurple);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token)).RootElement.Clone();

    private sealed class ScriptedModeration : IImageModerationService
    {
        private int _calls;

        public ImageModerationVerdict Verdict { get; set; } = ImageModerationVerdict.Approved;

        public int Calls => _calls;

        public Task<ImageModerationResult> ReviewAsync(
            ImageModerationRequest request,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            return Task.FromResult(new ImageModerationResult(Verdict));
        }
    }

    private sealed class RecordingStorage : IMediaStorage
    {
        public const string Url = "https://res.cloudinary.com/demo/image/upload/functional.webp";
        public const string PublicId = "pt-manager/trainers/functional/asset";

        public List<MediaUploadRequest> Uploads { get; } = [];

        public Task<MediaUploadOutcome> UploadAsync(MediaUploadRequest request, CancellationToken cancellationToken)
        {
            lock (Uploads)
                Uploads.Add(request);

            return Task.FromResult(new MediaUploadOutcome(
                MediaStorageStatus.Success, new StoredMedia(Url, PublicId)));
        }

        public Task<MediaDeletionOutcome> DeleteAsync(string publicId, Guid trainerId, CancellationToken cancellationToken) =>
            Task.FromResult(new MediaDeletionOutcome(MediaStorageStatus.Success));
    }
}
