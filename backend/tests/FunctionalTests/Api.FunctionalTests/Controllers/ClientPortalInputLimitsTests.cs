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
    [InlineData("a@b")]
    [InlineData("a b@c.pt")]
    [InlineData("a@c.pt.")]
    public async Task PatchMyProfile_EmailRejectedByDomain_ReturnsValidationError(string email)
    {
        var (trainerId, clientUserId) = await PortalTestData.SeedActiveClientAsync(
            _fixture.Factory, Token);
        var client = ClientPortalClient(trainerId, clientUserId);
        var payload = JsonSerializer.Serialize(new
        {
            contact_email = email,
            phone = "+351911111111",
            emergency_contact_name = (string?)null,
            emergency_contact_phone = (string?)null
        });

        var response = await PatchAsync(client, "/api/v1/portal/my-profile", payload);

        var body = await ReadBodyAsync(response);
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, body.ToString());
        Assert.Equal("validation_failed", body.GetProperty("title").GetString());
        var error = Assert.Single(body.GetProperty("errors").EnumerateArray());
        Assert.Equal("ContactEmail", error.GetProperty("field").GetString());
        Assert.Equal("client_email_invalid", error.GetProperty("code").GetString());
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
