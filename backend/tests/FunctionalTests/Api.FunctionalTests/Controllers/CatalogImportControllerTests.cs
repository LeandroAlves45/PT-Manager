using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Api.FunctionalTests.Support;

namespace Api.FunctionalTests.Controllers;

/// <summary>
/// Importação CSV do catálogo global pelo HTTP real: multipart, simulação, tudo ou nada,
/// reimportação sem duplicados e acesso só do superuser.
/// </summary>
[Collection(ApiTestCollection.Name)]
public sealed class CatalogImportControllerTests
{
    private const string FoodsImport = "/api/v1/global-foods/import";
    private const string ExercisesImport = "/api/v1/global-exercises/import";
    private const string FoodHeader = "nome;descricao;proteina;hidratos;gordura;fibra;porcao_g\n";

    private readonly PostgresApiFixture _fixture;

    public CatalogImportControllerTests(PostgresApiFixture fixture) => _fixture = fixture;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ImportFoods_DryRunThenImportThenReimport_CreatesOnceAndSkipsAfter()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var csv = FoodHeader + $"Arroz {suffix};;2,7;28;0,3;0,4;\nOvo {suffix};Cozido;12,6;1,1;10,6;;50\n";
        var client = await SuperuserClientAsync();

        var preview = await ReadJsonAsync(await client.PostAsync(FoodsImport, Form(csv, dryRun: true), Token));
        Assert.True(preview.GetProperty("dry_run").GetBoolean());
        Assert.Equal(2, preview.GetProperty("created").GetInt32());
        Assert.Equal(0, await CountAsync(client, "/api/v1/global-foods", suffix));

        var import = await ReadJsonAsync(await client.PostAsync(FoodsImport, Form(csv, dryRun: false), Token));
        Assert.False(import.GetProperty("dry_run").GetBoolean());
        Assert.Equal(2, import.GetProperty("total_rows").GetInt32());
        Assert.Equal(2, import.GetProperty("created").GetInt32());
        Assert.Equal(2, await CountAsync(client, "/api/v1/global-foods", suffix));

        var again = await ReadJsonAsync(await client.PostAsync(FoodsImport, Form(csv, dryRun: false), Token));
        Assert.Equal(0, again.GetProperty("created").GetInt32());
        var skipped = again.GetProperty("skipped").EnumerateArray().ToList();
        Assert.Equal(2, skipped.Count);
        Assert.All(skipped, row => Assert.Equal("already_exists", row.GetProperty("reason").GetString()));
        Assert.Equal(2, await CountAsync(client, "/api/v1/global-foods", suffix));
    }

    [Fact]
    public async Task ImportFoods_WithAnInvalidRow_ReturnsLineErrorsAndWritesNothing()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var csv = FoodHeader + $"Arroz {suffix};;2,7;28;0,3;;\nPão {suffix};;120;0;0;;\n";
        var client = await SuperuserClientAsync();

        var response = await client.PostAsync(FoodsImport, Form(csv, dryRun: false), Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await ReadJsonAsync(response, HttpStatusCode.BadRequest);
        var fields = problem.GetProperty("errors").EnumerateArray()
            .Select(error => error.GetProperty("field").GetString())
            .ToList();
        Assert.Contains("Rows[3].proteina", fields);
        Assert.Equal(0, await CountAsync(client, "/api/v1/global-foods", suffix));
    }

    [Fact]
    public async Task ImportFoods_WithoutFile_ReturnsFileError()
    {
        var client = await SuperuserClientAsync();
        using var content = new MultipartFormDataContent { { new StringContent("true"), "dry_run" } };

        var response = await client.PostAsync(FoodsImport, content, Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("catalog_import_file_empty", await response.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ImportFoods_FileAboveRequestLimit_IsRejectedBeforeTheHandler()
    {
        var client = await SuperuserClientAsync();
        var csv = FoodHeader + new string('a', 2 * 1024 * 1024);

        var response = await client.PostAsync(FoodsImport, Form(csv, dryRun: true), Token);

        // Como no logo: o TestServer aplica o limite multipart no model binding, antes do handler.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain("total_rows", await response.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ImportExercises_ValidFile_CreatesExercisesWithCanonicalDifficulty()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var csv = "nome;grupos_musculares;equipamento;dificuldade\n" +
            $"Supino {suffix};chest,triceps;Barra;intermedio\n";
        var client = await SuperuserClientAsync();

        var import = await ReadJsonAsync(await client.PostAsync(ExercisesImport, Form(csv, dryRun: false), Token));

        Assert.Equal(1, import.GetProperty("created").GetInt32());
        var list = await ReadJsonAsync(await client.GetAsync($"/api/v1/global-exercises?search={suffix}", Token));
        var exercise = Assert.Single(list.GetProperty("items").EnumerateArray().ToList());
        Assert.Equal("Intermédio", exercise.GetProperty("difficulty_level").GetString());
        Assert.Equal("chest,triceps", exercise.GetProperty("muscle_groups").GetString());
    }

    [Theory]
    [InlineData(FoodsImport, "trainer")]
    [InlineData(FoodsImport, "client")]
    [InlineData(ExercisesImport, "trainer")]
    [InlineData(ExercisesImport, "client")]
    public async Task Import_WithoutSuperuser_ReturnsForbidden(string path, string role)
    {
        var token = role == "trainer"
            ? TestJwtFactory.IssueTrainer(Guid.NewGuid())
            : TestJwtFactory.IssueClient(Guid.NewGuid(), Guid.NewGuid());
        var client = _fixture.Factory.CreateOriginClient().WithBearer(token);

        var response = await client.PostAsync(path, Form(FoodHeader + "Ovo;;12;1;10;;\n", dryRun: true), Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task<HttpClient> SuperuserClientAsync()
    {
        var superuserId = await NutritionTestData.SeedSuperuserAsync(_fixture.Factory, Token);
        return _fixture.Factory.CreateOriginClient().WithBearer(TestJwtFactory.IssueSuperuser(superuserId));
    }

    private static MultipartFormDataContent Form(string csv, bool dryRun)
    {
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        return new MultipartFormDataContent
        {
            { file, "file", "catalogo.csv" },
            { new StringContent(dryRun ? "true" : "false"), "dry_run" },
        };
    }

    private static async Task<int> CountAsync(HttpClient client, string path, string search)
    {
        var page = await ReadJsonAsync(await client.GetAsync($"{path}?search={search}", Token));
        return page.GetProperty("total_count").GetInt32();
    }

    private static async Task<JsonElement> ReadJsonAsync(
        HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var body = await response.Content.ReadAsStringAsync(Token);
        Assert.True(response.StatusCode == expected, $"{(int)response.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }
}
