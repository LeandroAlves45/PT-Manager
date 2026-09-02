using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.FunctionalTests.Support;

namespace Api.FunctionalTests.Controllers;

/// <summary>
/// Prova os seis endpoints de TrainerSettings contra PostgreSQL real.
/// </summary>
/// <remarks>
/// Nenhuma rota aceita identificador de personal trainer: as definições alvo derivam
/// sempre do tenant estabelecido a partir das claims. Os testes de tenant confirmam-no
/// mostrando que dois trainers autenticados vêem estados independentes.
/// </remarks>
[Collection(ApiTestCollection.Name)]
public sealed class TrainerSettingsControllerTests
{
    private readonly ApiWebApplicationFactory _factory;

    public TrainerSettingsControllerTests(PostgresApiFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _factory = fixture.Factory;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // ---------------------------------------------------------- GET /trainer-settings

    [Fact]
    public async Task Get_WithTrainerToken_ReturnsTheDefaultsCreatedAtOnboarding()
    {
        var trainer = await SeedTrainerAsync("settings-get");

        var response = await TrainerClient(trainer.TrainerId)
            .GetAsync("/api/v1/trainer-settings", Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await ReadJsonAsync(response);
        Assert.Equal("PT Manager", body.GetProperty("app_name").GetString());
        Assert.Equal("Europe/Lisbon", body.GetProperty("timezone").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("logo_url").ValueKind);
    }

    [Fact]
    public async Task Get_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _factory.CreateOriginClient()
            .GetAsync("/api/v1/trainer-settings", Token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithClientRole_ReturnsForbidden()
    {
        var caller = _factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueClient(Guid.NewGuid(), Guid.NewGuid()));

        var response = await caller.GetAsync("/api/v1/trainer-settings", Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithSuperuserRole_ReturnsForbidden()
    {
        var caller = _factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueSuperuser(Guid.NewGuid()));

        var response = await caller.GetAsync("/api/v1/trainer-settings", Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Get_IsScopedToTheAuthenticatedTenant()
    {
        var first = await SeedTrainerAsync("settings-tenant-first");
        var second = await SeedTrainerAsync("settings-tenant-second");

        await TrainerClient(first.TrainerId).PatchAsJsonAsync(
            "/api/v1/trainer-settings/branding",
            new { app_name = "Estudio Primeiro", primary_color = "#112233" },
            Token);

        var secondSettings = await ReadJsonAsync(
            await TrainerClient(second.TrainerId)
                .GetAsync("/api/v1/trainer-settings", Token));

        // A escrita do primeiro tenant não pode ser observável pelo segundo.
        Assert.Equal("PT Manager", secondSettings.GetProperty("app_name").GetString());
        Assert.Equal(
            JsonValueKind.Null,
            secondSettings.GetProperty("primary_color").ValueKind);
    }

    // ------------------------------------------------ PATCH /trainer-settings/branding

    [Fact]
    public async Task UpdateBranding_WithValidPayload_ReturnsUpdatedSettings()
    {
        var trainer = await SeedTrainerAsync("branding-happy");

        var response = await TrainerClient(trainer.TrainerId).PatchAsJsonAsync(
            "/api/v1/trainer-settings/branding",
            new
            {
                app_name = "Estudio Alfa",
                primary_color = "#AABBCC",
                body_color = "#112233"
            },
            Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await ReadJsonAsync(response);
        Assert.Equal("Estudio Alfa", body.GetProperty("app_name").GetString());
        Assert.Equal("#AABBCC", body.GetProperty("primary_color").GetString());
        Assert.Equal("#112233", body.GetProperty("body_color").GetString());
    }

    [Fact]
    public async Task UpdateBranding_WithInvalidColor_ReturnsBadRequestWithErrorsArray()
    {
        var trainer = await SeedTrainerAsync("branding-invalid");

        var response = await TrainerClient(trainer.TrainerId).PatchAsJsonAsync(
            "/api/v1/trainer-settings/branding",
            new { app_name = "Estudio", primary_color = "vermelho" },
            Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await ReadJsonAsync(response);
        var codes = body.GetProperty("errors").EnumerateArray()
            .Select(error => error.GetProperty("code").GetString())
            .ToArray();
        Assert.Contains("trainer_settings_primary_color_invalid", codes);
    }

    [Fact]
    public async Task UpdateBranding_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _factory.CreateOriginClient().PatchAsJsonAsync(
            "/api/v1/trainer-settings/branding",
            new { app_name = "Sem Token" },
            Token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateBranding_WithClientRole_ReturnsForbidden()
    {
        var caller = _factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueClient(Guid.NewGuid(), Guid.NewGuid()));

        var response = await caller.PatchAsJsonAsync(
            "/api/v1/trainer-settings/branding",
            new { app_name = "Role Errado" },
            Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ------------------------------- POST /trainer-settings/branding/reset-colors

    [Fact]
    public async Task ResetBrandingColors_ClearsBothColorsAndKeepsAppName()
    {
        var trainer = await SeedTrainerAsync("reset-colors");
        var caller = TrainerClient(trainer.TrainerId);
        await caller.PatchAsJsonAsync(
            "/api/v1/trainer-settings/branding",
            new { app_name = "Estudio Beta", primary_color = "#AABBCC", body_color = "#DDEEFF" },
            Token);

        var response = await caller.PostAsync(
            "/api/v1/trainer-settings/branding/reset-colors",
            content: null,
            Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await ReadJsonAsync(response);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("primary_color").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("body_color").ValueKind);
        Assert.Equal("Estudio Beta", body.GetProperty("app_name").GetString());
    }

    [Fact]
    public async Task ResetBrandingColors_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _factory.CreateOriginClient().PostAsync(
            "/api/v1/trainer-settings/branding/reset-colors",
            content: null,
            Token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ResetBrandingColors_WithClientRole_ReturnsForbidden()
    {
        var caller = _factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueClient(Guid.NewGuid(), Guid.NewGuid()));

        var response = await caller.PostAsync(
            "/api/v1/trainer-settings/branding/reset-colors",
            content: null,
            Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // -------------------------------------------------- DELETE /trainer-settings/logo

    [Fact]
    public async Task RemoveLogo_ReturnsOkWithTheResultingSettings()
    {
        var trainer = await SeedTrainerAsync("remove-logo");

        var response = await TrainerClient(trainer.TrainerId)
            .DeleteAsync("/api/v1/trainer-settings/logo", Token);

        // 200 e não 204: o caso de uso devolve o estado resultante para evitar
        // um segundo pedido do cliente.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await ReadJsonAsync(response);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("logo_url").ValueKind);
    }

    [Fact]
    public async Task RemoveLogo_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _factory.CreateOriginClient()
            .DeleteAsync("/api/v1/trainer-settings/logo", Token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RemoveLogo_WithClientRole_ReturnsForbidden()
    {
        var caller = _factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueClient(Guid.NewGuid(), Guid.NewGuid()));

        var response = await caller.DeleteAsync("/api/v1/trainer-settings/logo", Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ------------------------------------------------ PATCH /trainer-settings/contacts

    [Fact]
    public async Task UpdateContacts_WithValidPayload_ReturnsUpdatedSettings()
    {
        var trainer = await SeedTrainerAsync("contacts-happy");

        var response = await TrainerClient(trainer.TrainerId).PatchAsJsonAsync(
            "/api/v1/trainer-settings/contacts",
            new { phone = "+351911222333", address = "Rua do Teste 1", city = "Porto" },
            Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await ReadJsonAsync(response);
        Assert.Equal("+351911222333", body.GetProperty("phone").GetString());
        Assert.Equal("Rua do Teste 1", body.GetProperty("address").GetString());
        Assert.Equal("Porto", body.GetProperty("city").GetString());
    }

    [Fact]
    public async Task UpdateContacts_WithOversizedPhone_ReturnsBadRequestWithErrorsArray()
    {
        var trainer = await SeedTrainerAsync("contacts-invalid");

        var response = await TrainerClient(trainer.TrainerId).PatchAsJsonAsync(
            "/api/v1/trainer-settings/contacts",
            new { phone = new string('9', 21) },
            Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await ReadJsonAsync(response);
        var codes = body.GetProperty("errors").EnumerateArray()
            .Select(error => error.GetProperty("code").GetString())
            .ToArray();
        Assert.Contains("trainer_settings_phone_too_long", codes);
    }

    [Fact]
    public async Task UpdateContacts_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _factory.CreateOriginClient().PatchAsJsonAsync(
            "/api/v1/trainer-settings/contacts",
            new { phone = "+351911222333" },
            Token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateContacts_WithClientRole_ReturnsForbidden()
    {
        var caller = _factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueClient(Guid.NewGuid(), Guid.NewGuid()));

        var response = await caller.PatchAsJsonAsync(
            "/api/v1/trainer-settings/contacts",
            new { phone = "+351911222333" },
            Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ------------------------------------------------ PATCH /trainer-settings/timezone

    [Fact]
    public async Task ChangeTimezone_WithKnownIanaIdentifier_ReturnsUpdatedSettings()
    {
        var trainer = await SeedTrainerAsync("timezone-happy");

        var response = await TrainerClient(trainer.TrainerId).PatchAsJsonAsync(
            "/api/v1/trainer-settings/timezone",
            new { timezone = "Europe/Madrid" },
            Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await ReadJsonAsync(response);
        Assert.Equal("Europe/Madrid", body.GetProperty("timezone").GetString());
    }

    [Fact]
    public async Task ChangeTimezone_WithUnknownIdentifier_ReturnsBadRequestWithErrorsArray()
    {
        var trainer = await SeedTrainerAsync("timezone-invalid");

        var response = await TrainerClient(trainer.TrainerId).PatchAsJsonAsync(
            "/api/v1/trainer-settings/timezone",
            new { timezone = "Marte/Olympus" },
            Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await ReadJsonAsync(response);
        var codes = body.GetProperty("errors").EnumerateArray()
            .Select(error => error.GetProperty("code").GetString())
            .ToArray();
        Assert.Contains("trainer_settings_invalid_timezone", codes);
    }

    [Fact]
    public async Task ChangeTimezone_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _factory.CreateOriginClient().PatchAsJsonAsync(
            "/api/v1/trainer-settings/timezone",
            new { timezone = "Europe/Madrid" },
            Token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ChangeTimezone_WithClientRole_ReturnsForbidden()
    {
        var caller = _factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueClient(Guid.NewGuid(), Guid.NewGuid()));

        var response = await caller.PatchAsJsonAsync(
            "/api/v1/trainer-settings/timezone",
            new { timezone = "Europe/Madrid" },
            Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ------------------------------------------------------------------- utilitários

    private Task<SeededTrainer> SeedTrainerAsync(string discriminator) =>
        TrainerTenantSeeder.SeedTrainerAsync(_factory, discriminator, Token);

    private HttpClient TrainerClient(Guid trainerId) =>
        _factory.CreateOriginClient().WithBearer(TestJwtFactory.IssueTrainer(trainerId));

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadAsStringAsync(Token);
        return JsonDocument.Parse(payload).RootElement.Clone();
    }
}
