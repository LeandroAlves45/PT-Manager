using System.Net;
using Application.Common.Abstractions;
using Infrastructure.IntegrationTests.Support;
using Infrastructure.Media.Cloudinary;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Infrastructure.IntegrationTests.Media;

/// <summary>
/// Prova o contrato do adapter Cloudinary: assinatura, identificadores gerados
/// pelo servidor, pasta tenant-safe, validação da resposta, eliminação
/// idempotente e classificação de falhas.
/// </summary>
public sealed class CloudinaryMediaStorageTests
{
    private static readonly DateTime Now = new(2026, 9, 11, 10, 0, 0, DateTimeKind.Utc);
    private static readonly Guid TrainerId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private const string ApiSecret = "unit-test-secret";

    private static string Folder => $"pt-manager/test/trainers/{TrainerId:N}/logos";

    [Fact]
    public void Signature_MatchesTheDocumentedProviderVector()
    {
        // Exemplo publicado em cloudinary.com/documentation/authentication_signatures.
        var signature = CloudinarySignature.Sign(
            new Dictionary<string, string>
            {
                ["timestamp"] = "1315060510",
                ["public_id"] = "sample_image",
                ["eager"] = "w_400,h_300,c_pad|w_260,h_200,c_crop",
                ["file"] = "ignored",
                ["api_key"] = "ignored",
                ["cloud_name"] = "ignored",
                ["resource_type"] = "ignored"
            },
            "abcd");

        Assert.Equal("bfd09f95f331f558cbd1320e67aa8d488770583e", signature);
    }

    [Fact]
    public void Signature_ChangesWhenAnySignedParameterChanges()
    {
        var baseline = CloudinarySignature.Sign(new Dictionary<string, string> { ["timestamp"] = "1" }, "s");
        var tampered = CloudinarySignature.Sign(
            new Dictionary<string, string> { ["timestamp"] = "1", ["folder"] = "x" }, "s");

        Assert.NotEqual(baseline, tampered);
    }

    [Fact]
    public async Task Upload_WhenResponseBodyExceedsTimeout_ReturnsTransientFailure()
    {
        var stub = new MediaHttpStub().Respond(new DelayedMediaContent(UploadBody($"{Folder}/slow")));
        using var client = stub.CreateClient("https://api.cloudinary.com/");
        client.Timeout = TimeSpan.FromMilliseconds(100);

        var result = await CreateStorage(stub, client: client).UploadAsync(
            Request(), TestContext.Current.CancellationToken);

        Assert.Equal(MediaStorageStatus.TransientFailure, result.Status);
        Assert.Equal("cloudinary_timeout", result.FailureCode);
    }

    [Fact]
    public async Task Upload_WhenDisabled_DoesNoIo()
    {
        var stub = new MediaHttpStub();

        var outcome = await CreateStorage(stub, enabled: false).UploadAsync(
            Request(), TestContext.Current.CancellationToken);

        Assert.Equal(MediaStorageStatus.Disabled, outcome.Status);
        Assert.Empty(stub.Requests);
    }

    [Fact]
    public async Task Upload_SendsASignedRequestWithServerGeneratedIdentifier()
    {
        var stub = new MediaHttpStub().Respond(HttpStatusCode.OK, UploadBody($"{Folder}/abc123"));

        var outcome = await CreateStorage(stub).UploadAsync(Request(), TestContext.Current.CancellationToken);

        Assert.Equal(MediaStorageStatus.Success, outcome.Status);
        Assert.Equal($"{Folder}/abc123", outcome.Media!.PublicId);

        var request = Assert.Single(stub.Requests);
        Assert.Equal("/v1_1/demo-cloud/image/upload", request.Uri.AbsolutePath);
        Assert.Equal(Folder, request.Fields["folder"]);
        Assert.Equal("false", request.Fields["overwrite"]);
        Assert.Equal("false", request.Fields["use_filename"]);
        Assert.Equal("true", request.Fields["unique_filename"]);
        Assert.Equal("123456", request.Fields["api_key"]);

        // A assinatura enviada é a que o protocolo exige sobre os campos enviados.
        var signed = request.Fields
            .Where(field => field.Key is not ("api_key" or "signature"))
            .ToDictionary(field => field.Key, field => field.Value);
        Assert.Equal(CloudinarySignature.Sign(signed, ApiSecret), request.Fields["signature"]);

        // O identificador é do servidor e o segredo nunca viaja.
        Assert.False(request.Fields.ContainsKey("public_id"));
        Assert.DoesNotContain(ApiSecret, request.Body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("pt-manager/test/trainers/99999999888877776666555555555555/logos/x", "https://res.cloudinary.com/demo/x.webp")]
    [InlineData("pt-manager/test/trainers/11111111222233334444555555555555/logos/x", "http://res.cloudinary.com/demo/x.webp")]
    [InlineData("pt-manager/test/trainers/11111111222233334444555555555555/logos/x", "https://evil.example.com/x.webp")]
    [InlineData("", "https://res.cloudinary.com/demo/x.webp")]
    public async Task Upload_WhenResponseViolatesTheContract_ReturnsInvalidResponse(string publicId, string url)
    {
        var stub = new MediaHttpStub().Respond(
            HttpStatusCode.OK, $$"""{"public_id":"{{publicId}}","secure_url":"{{url}}"}""");

        var outcome = await CreateStorage(stub).UploadAsync(Request(), TestContext.Current.CancellationToken);

        Assert.Equal(MediaStorageStatus.InvalidResponse, outcome.Status);
        Assert.Null(outcome.Media);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, MediaStorageStatus.TransientFailure)]
    [InlineData(HttpStatusCode.TooManyRequests, MediaStorageStatus.TransientFailure)]
    [InlineData((HttpStatusCode)420, MediaStorageStatus.TransientFailure)]
    [InlineData(HttpStatusCode.BadRequest, MediaStorageStatus.PermanentFailure)]
    [InlineData(HttpStatusCode.Unauthorized, MediaStorageStatus.PermanentFailure)]
    public async Task Upload_ClassifiesProviderFailures(HttpStatusCode status, MediaStorageStatus expected)
    {
        var stub = new MediaHttpStub().Respond(status, """{"error":{"message":"echo of request"}}""");

        var outcome = await CreateStorage(stub).UploadAsync(Request(), TestContext.Current.CancellationToken);

        Assert.Equal(expected, outcome.Status);
        Assert.Equal($"cloudinary_http_{(int)status}", outcome.FailureCode);
    }

    [Fact]
    public async Task Upload_WhenNetworkFails_IsTransient()
    {
        var stub = new MediaHttpStub().Throw(new HttpRequestException("down"));

        var outcome = await CreateStorage(stub).UploadAsync(Request(), TestContext.Current.CancellationToken);

        Assert.Equal(MediaStorageStatus.TransientFailure, outcome.Status);
    }

    [Fact]
    public async Task Upload_WhenCallerCancels_PropagatesCancellation()
    {
        var stub = new MediaHttpStub().Respond(HttpStatusCode.OK, UploadBody($"{Folder}/x"));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => CreateStorage(stub).UploadAsync(Request(), cancellation.Token));
    }

    [Theory]
    [InlineData("""{"result":"ok"}""")]
    [InlineData("""{"result":"not found"}""")]
    public async Task Delete_IsIdempotent(string body)
    {
        var stub = new MediaHttpStub().Respond(HttpStatusCode.OK, body);

        var outcome = await CreateStorage(stub).DeleteAsync(
            $"{Folder}/abc123", TrainerId, TestContext.Current.CancellationToken);

        Assert.Equal(MediaStorageStatus.Success, outcome.Status);
        var request = Assert.Single(stub.Requests);
        Assert.Equal("/v1_1/demo-cloud/image/destroy", request.Uri.AbsolutePath);
        Assert.Equal("true", request.Fields["invalidate"]);
        Assert.Equal($"{Folder}/abc123", request.Fields["public_id"]);
    }

    /// <summary>
    /// Ponto 7 do Gate 5C: um identificador de outro tenant, ou fora da raiz
    /// gerida, é recusado antes de qualquer I/O.
    /// </summary>
    [Theory]
    [InlineData("pt-manager/test/trainers/99999999888877776666555555555555/logos/abc")]
    [InlineData("someone-else/sample")]
    [InlineData("pt-manager/test/trainers/11111111222233334444555555555555/../../x")]
    public async Task Delete_RefusesIdentifiersOutsideTheTenantFolder(string publicId)
    {
        var stub = new MediaHttpStub();

        var outcome = await CreateStorage(stub).DeleteAsync(
            publicId, TrainerId, TestContext.Current.CancellationToken);

        Assert.Equal(MediaStorageStatus.PermanentFailure, outcome.Status);
        Assert.Equal("cloudinary_public_id_not_owned", outcome.FailureCode);
        Assert.Empty(stub.Requests);
    }

    private static CloudinaryMediaStorage CreateStorage(MediaHttpStub stub, bool enabled = true, HttpClient? client = null) =>
        new(
            client ?? stub.CreateClient("https://api.cloudinary.com/"),
            Options.Create(new CloudinaryOptions
            {
                Enabled = enabled,
                CloudName = "demo-cloud",
                ApiKey = "123456",
                ApiSecret = ApiSecret,
                FolderRoot = "pt-manager/test"
            }),
            new TestClock(Now),
            NullLogger<CloudinaryMediaStorage>.Instance);

    private static MediaUploadRequest Request() =>
        new(new byte[] { 1, 2, 3 }, "image/webp", MediaAssetKind.TrainerLogo, TrainerId);

    private static string UploadBody(string publicId) =>
        $$"""{"public_id":"{{publicId}}","secure_url":"https://res.cloudinary.com/demo-cloud/image/upload/v1/{{publicId}}.webp"}""";
}
