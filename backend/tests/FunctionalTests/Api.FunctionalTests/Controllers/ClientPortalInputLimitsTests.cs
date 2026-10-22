using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Api.FunctionalTests.Support;

namespace Api.FunctionalTests.Controllers;

/// <summary>
/// Valores-limite do perfil e da resposta ao check-in no portal: o que antes devolvia 500 passa
/// a 400 com um código estável, e ficam fixados os contratos de erro que o frontend lê.
/// </summary>
[Collection(ApiTestCollection.Name)]
public sealed class ClientPortalInputLimitsTests
{
    private const string EmptyAnswerTail =
        "\"body_fat_percentage\":null,\"notes\":null,\"feedback\":null," +
        "\"training_adherence_score\":null,\"nutrition_adherence_score\":null";

    private readonly PostgresApiFixture _fixture;

    public ClientPortalInputLimitsTests(PostgresApiFixture fixture) => _fixture = fixture;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task PatchMyProfile_EmptyName_ReturnsValidationError(string name)
    {
        var (trainerId, clientUserId) = await PortalTestData.SeedActiveClientAsync(
            _fixture.Factory, Token);
        var client = ClientPortalClient(trainerId, clientUserId);
        var payload = JsonSerializer.Serialize(new
        {
            name,
            phone = "+351911111111",
            emergency_contact_name = (string?)null,
            emergency_contact_phone = (string?)null
        });

        var response = await PatchAsync(client, "/api/v1/portal/my-profile", payload);

        var body = await ReadBodyAsync(response);
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, body.ToString());
        Assert.Equal("validation_failed", body.GetProperty("title").GetString());
        var error = Assert.Single(body.GetProperty("errors").EnumerateArray());
        Assert.Equal("Name", error.GetProperty("field").GetString());
        Assert.Equal("client_name_invalid", error.GetProperty("code").GetString());
    }

    [Fact]
    public async Task PatchMyProfile_NullCharacterInName_ReturnsValidationError()
    {
        var (trainerId, clientUserId) = await PortalTestData.SeedActiveClientAsync(
            _fixture.Factory, Token);
        var client = ClientPortalClient(trainerId, clientUserId);
        const string payload =
            "{\"name\":\"Ana\\u0000\",\"phone\":\"+351911111111\"," +
            "\"emergency_contact_name\":null,\"emergency_contact_phone\":null}";

        var response = await PatchAsync(client, "/api/v1/portal/my-profile", payload);

        var body = await ReadBodyAsync(response);
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, body.ToString());
        var error = Assert.Single(body.GetProperty("errors").EnumerateArray());
        Assert.Equal("Name", error.GetProperty("field").GetString());
        Assert.Equal("text_invalid_character", error.GetProperty("code").GetString());
    }

    [Fact]
    public async Task SubmitCheckInResponse_WeightAboveMaximum_ReturnsValidationError()
    {
        var (client, url) = await DueCheckInAsync();

        var response = await PostAsync(
            client, url, "{\"weight_kg\":1000.01,\"body_measurements\":null," + EmptyAnswerTail + "}");

        var body = await ReadBodyAsync(response);
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, body.ToString());
        var error = Assert.Single(body.GetProperty("errors").EnumerateArray());
        Assert.Equal("WeightKg", error.GetProperty("field").GetString());
        Assert.Equal("weight_invalid", error.GetProperty("code").GetString());
    }

    [Fact]
    public async Task SubmitCheckInResponse_WeightAtMaximum_ReturnsOk()
    {
        var (client, url) = await DueCheckInAsync();

        var response = await PostAsync(
            client, url, "{\"weight_kg\":1000,\"body_measurements\":null," + EmptyAnswerTail + "}");

        var body = await ReadBodyAsync(response);
        Assert.True(response.StatusCode == HttpStatusCode.OK, body.ToString());
        Assert.Equal("answered", body.GetProperty("status").GetString());
    }

    // As colunas são numeric(10,2): 0.004 gravava-se como 0.00 e 99.996 como 100.00, fora dos
    // checks da tabela (500). Com três casas, o reenvio igual deixava de ser reconhecido (409).
    [Theory]
    [InlineData("0.004", "null", "WeightKg", "weight_invalid")]
    [InlineData("71.555", "null", "WeightKg", "weight_invalid")]
    [InlineData("70", "99.996", "BodyFatPercentage", "body_fat_percentage_invalid")]
    [InlineData("70", "17.555", "BodyFatPercentage", "body_fat_percentage_invalid")]
    public async Task SubmitCheckInResponse_MoreThanTwoDecimals_ReturnsValidationError(
        string weightKg, string bodyFat, string field, string code)
    {
        var (client, url) = await DueCheckInAsync();
        var payload =
            "{\"weight_kg\":" + weightKg + ",\"body_fat_percentage\":" + bodyFat + "," +
            "\"body_measurements\":null,\"notes\":null,\"feedback\":null," +
            "\"training_adherence_score\":null,\"nutrition_adherence_score\":null}";

        var response = await PostAsync(client, url, payload);

        var body = await ReadBodyAsync(response);
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, body.ToString());
        var error = Assert.Single(body.GetProperty("errors").EnumerateArray());
        Assert.Equal(field, error.GetProperty("field").GetString());
        Assert.Equal(code, error.GetProperty("code").GetString());
    }

    // O PostgreSQL não guarda U+0000 em text nem em jsonb: sem a regra, a escrita dava 500.
    [Fact]
    public async Task SubmitCheckInResponse_NullCharacterInNotes_ReturnsValidationError()
    {
        var (client, url) = await DueCheckInAsync();
        var payload =
            "{\"weight_kg\":70,\"body_fat_percentage\":null,\"body_measurements\":null," +
            "\"notes\":\"a\\u0000b\",\"feedback\":null," +
            "\"training_adherence_score\":null,\"nutrition_adherence_score\":null}";

        var response = await PostAsync(client, url, payload);

        var body = await ReadBodyAsync(response);
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, body.ToString());
        var error = Assert.Single(body.GetProperty("errors").EnumerateArray());
        Assert.Equal("Notes", error.GetProperty("field").GetString());
        Assert.Equal("text_invalid_character", error.GetProperty("code").GetString());
    }

    [Fact]
    public async Task PatchMyProfile_NullCharacterInPhone_ReturnsValidationError()
    {
        var (trainerId, clientUserId) = await PortalTestData.SeedActiveClientAsync(
            _fixture.Factory, Token);
        var client = ClientPortalClient(trainerId, clientUserId);
        const string payload =
            "{\"name\":\"Portal client\",\"phone\":\"+351\\u0000911\"," +
            "\"emergency_contact_name\":null,\"emergency_contact_phone\":null}";

        var response = await PatchAsync(client, "/api/v1/portal/my-profile", payload);

        var body = await ReadBodyAsync(response);
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, body.ToString());
        var error = Assert.Single(body.GetProperty("errors").EnumerateArray());
        Assert.Equal("Phone", error.GetProperty("field").GetString());
        Assert.Equal("text_invalid_character", error.GetProperty("code").GetString());
    }

    [Fact]
    public async Task SubmitCheckInResponse_InvalidMeasurement_ReportsItsPositionInTheField()
    {
        var (client, url) = await DueCheckInAsync();
        const string measurements =
            "{\"waist_cm\":80,\"hip_cm\":null,\"chest_cm\":-1,\"right_arm_cm\":null," +
            "\"left_arm_cm\":null,\"right_thigh_cm\":null,\"left_thigh_cm\":null," +
            "\"right_calf_cm\":null,\"left_calf_cm\":null}";

        var response = await PostAsync(
            client, url, "{\"weight_kg\":70,\"body_measurements\":" + measurements + "," + EmptyAnswerTail + "}");

        var body = await ReadBodyAsync(response);
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, body.ToString());
        var error = Assert.Single(body.GetProperty("errors").EnumerateArray());
        // O índice segue a ordem do contrato: 0 cintura, 1 anca, 2 peito, …
        Assert.Equal("BodyMeasurements[2]", error.GetProperty("field").GetString());
        Assert.Equal("body_measurement_invalid", error.GetProperty("code").GetString());
    }

    [Fact]
    public async Task SubmitCheckInResponse_SameAnswerTwice_IsIdempotent_AndADifferentOneConflicts()
    {
        var (client, url) = await DueCheckInAsync();
        var answer = "{\"weight_kg\":71.5,\"body_measurements\":null," + EmptyAnswerTail + "}";

        var first = await PostAsync(client, url, answer);
        var second = await PostAsync(client, url, answer);
        var different = await PostAsync(
            client, url, "{\"weight_kg\":72,\"body_measurements\":null," + EmptyAnswerTail + "}");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var body = await ReadBodyAsync(different);
        Assert.True(different.StatusCode == HttpStatusCode.Conflict, body.ToString());
        Assert.Equal("check_in_already_answered", body.GetProperty("title").GetString());
    }

    [Fact]
    public async Task AnsweredCheckIn_IsNoLongerDueNorNext()
    {
        var (client, url) = await DueCheckInAsync();
        await PostAsync(client, url, "{\"weight_kg\":70,\"body_measurements\":null," + EmptyAnswerTail + "}");

        var due = await client.GetAsync("/api/v1/portal/my-check-ins/due", Token);
        var next = await client.GetAsync("/api/v1/portal/my-check-ins/next", Token);

        Assert.Equal(HttpStatusCode.NotFound, due.StatusCode);
        Assert.Equal("resource_not_found", (await ReadBodyAsync(due)).GetProperty("title").GetString());
        Assert.Equal(HttpStatusCode.NotFound, next.StatusCode);
        Assert.Equal("resource_not_found", (await ReadBodyAsync(next)).GetProperty("title").GetString());
    }

    private async Task<(HttpClient Client, string Url)> DueCheckInAsync()
    {
        var (trainerId, clientUserId) = await PortalTestData.SeedActiveClientAsync(
            _fixture.Factory, Token);
        var client = ClientPortalClient(trainerId, clientUserId);
        var due = await ReadBodyAsync(
            await client.GetAsync("/api/v1/portal/my-check-ins/due", Token));

        return (client, $"/api/v1/portal/check-ins/{due.GetProperty("id").GetGuid()}/respond");
    }

    private static async Task<HttpResponseMessage> PostAsync(
        HttpClient client, string url, string payload)
    {
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        return await client.PostAsync(url, content, Token);
    }

    private static async Task<HttpResponseMessage> PatchAsync(
        HttpClient client, string url, string payload)
    {
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        return await client.PatchAsync(url, content, Token);
    }

    private HttpClient ClientPortalClient(Guid trainerId, Guid clientUserId) =>
        _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueClient(clientUserId, trainerId));

    private static Task<JsonElement> ReadBodyAsync(HttpResponseMessage response) =>
        response.Content.ReadFromJsonAsync<JsonElement>(ApiJsonPayload.Options, Token);
}
