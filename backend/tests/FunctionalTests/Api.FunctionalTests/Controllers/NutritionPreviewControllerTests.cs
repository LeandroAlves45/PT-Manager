using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.FunctionalTests.Support;

namespace Api.FunctionalTests.Controllers;

/// <summary>Prova que o cálculo puro é restrito na fronteira HTTP e não no handler.</summary>
[Collection(ApiTestCollection.Name)]
public sealed class NutritionPreviewControllerTests
{
    private readonly PostgresApiFixture _fixture;

    public NutritionPreviewControllerTests(PostgresApiFixture fixture) => _fixture = fixture;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Preview_WithTrainerToken_ReturnsCalculation()
    {
        var client = _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueTrainer(Guid.NewGuid()));

        var response = await client.PostAsJsonAsync(
            "/api/v1/nutrition/preview",
            new { calculation = NutritionHttpPayloads.GramsPerKgPreviewCalculation() },
            Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await ReadJsonAsync(response);
        Assert.Equal(2400m, body.GetProperty("target_kcal").GetDecimal());
        Assert.Equal(160m, body.GetProperty("protein_target_grams").GetDecimal());
    }

    [Fact]
    public async Task Preview_WithClientToken_ReturnsForbidden()
    {
        var client = _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueClient(Guid.NewGuid(), Guid.NewGuid()));

        var response = await client.PostAsJsonAsync(
            "/api/v1/nutrition/preview",
            new { calculation = NutritionHttpPayloads.GramsPerKgPreviewCalculation() },
            Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Preview_ExecutesNoDatabaseCommands()
    {
        var client = _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueTrainer(Guid.NewGuid()));

        using var scope = CommandCountingInterceptor.BeginScope();

        await client.PostAsJsonAsync(
            "/api/v1/nutrition/preview",
            new { calculation = NutritionHttpPayloads.GramsPerKgPreviewCalculation() },
            Token);

        Assert.Equal(0, scope.Count);
    }

    [Fact]
    public async Task Preview_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _fixture.Factory.CreateOriginClient().PostAsJsonAsync(
            "/api/v1/nutrition/preview",
            new { calculation = NutritionHttpPayloads.GramsPerKgPreviewCalculation() },
            Token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadAsStringAsync(Token);
        return JsonDocument.Parse(payload).RootElement.Clone();
    }
}
