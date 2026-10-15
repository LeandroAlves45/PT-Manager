using System.Net;
using System.Text;
using System.Text.Json;
using Api.FunctionalTests.Support;

namespace Api.FunctionalTests.Controllers;

/// <summary>
/// O PostgreSQL não guarda U+0000 em <c>text</c>: um email ou nome com esse carácter tem de
/// ser recusado pelo validator com 400, antes de chegar à base de dados (senão, 500 anónimo).
/// </summary>
[Collection(ApiTestCollection.Name)]
public sealed class AuthNullCharacterTests : IAsyncLifetime
{
    private readonly PostgresApiFixture _database;
    private ApiWebApplicationFactory _factory = null!;

    public AuthNullCharacterTests(PostgresApiFixture database) => _database = database;

    public ValueTask InitializeAsync()
    {
        // Factory por classe: os rate limiters vivem no host.
        _factory = new ApiWebApplicationFactory(_database.ConnectionString);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _factory.Dispose();
        return ValueTask.CompletedTask;
    }

    [Theory]
    [InlineData("/api/v1/auth/signup",
        "{\"email\":\"a\\u0000b@example.com\",\"password\":\"Str0ng!Pass\",\"full_name\":\"Ana\"}",
        "Email", "authentication_email_invalid")]
    [InlineData("/api/v1/auth/signup",
        "{\"email\":\"nul-name@example.com\",\"password\":\"Str0ng!Pass\",\"full_name\":\"A\\u0000na\"}",
        "FullName", "text_invalid_character")]
    [InlineData("/api/v1/auth/email-confirmation/request",
        "{\"email\":\"a\\u0000b@example.com\"}",
        "Email", "authentication_email_invalid")]
    [InlineData("/api/v1/auth/password-reset/request",
        "{\"email\":\"a\\u0000b@example.com\"}",
        "Email", "authentication_email_invalid")]
    [InlineData("/api/v1/auth/login",
        "{\"email\":\"a\\u0000b@example.com\",\"password\":\"Str0ng!Pass\"}",
        "Email", "authentication_email_invalid")]
    public async Task AnonymousAuthEndpoint_NullCharacter_ReturnsValidationError(
        string route, string payload, string field, string code)
    {
        // Cada cliente recebe um IP próprio da factory: as políticas de auth não se cruzam.
        var client = _factory.CreateOriginClient();

        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var response = await client.PostAsync(route, content, TestContext.Current.CancellationToken);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, body);
        using var document = JsonDocument.Parse(body);
        var error = Assert.Single(document.RootElement.GetProperty("errors").EnumerateArray());
        Assert.Equal(field, error.GetProperty("field").GetString());
        Assert.Equal(code, error.GetProperty("code").GetString());
    }
}
