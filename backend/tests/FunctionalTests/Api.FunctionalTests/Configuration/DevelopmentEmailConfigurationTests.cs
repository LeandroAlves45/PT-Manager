using System.Net;
using System.Net.Http.Json;
using Api.FunctionalTests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Api.FunctionalTests.Configuration;

/// <summary>
/// <c>DevelopmentEmail:LogLinks</c> troca o envio por log. Fora de Development o arranque
/// recusa-o, porque o link do log é uma credencial de conta.
/// </summary>
[Collection(ApiTestCollection.Name)]
public sealed class DevelopmentEmailConfigurationTests(PostgresApiFixture database)
{
    private const string LogLinksSetting = "DevelopmentEmail:LogLinks";

    [Theory]
    [InlineData("Production")]
    [InlineData("Testing")]
    [InlineData("Staging")]
    public void LogLinks_OutsideDevelopment_StopsTheHostFromStarting(string environment)
    {
        using var factory = new ApiWebApplicationFactory(database.ConnectionString, environment)
        {
            AdditionalSettings = Settings(logLinks: "true")
        };

        var failure = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains(LogLinksSetting, Flatten(failure), StringComparison.Ordinal);
    }

    [Fact]
    public void LogLinksAbsent_InProduction_StartsNormally()
    {
        using var factory = new ApiWebApplicationFactory(database.ConnectionString, "Production")
        {
            AdditionalSettings = Settings(logLinks: null)
        };

        using var client = factory.CreateClient();

        Assert.NotNull(client);
    }

    [Fact]
    public void LogLinksFalse_InProduction_StartsNormally()
    {
        using var factory = new ApiWebApplicationFactory(database.ConnectionString, "Production")
        {
            AdditionalSettings = Settings(logLinks: "false")
        };

        using var client = factory.CreateClient();

        Assert.NotNull(client);
    }

    [Fact]
    public async Task LogLinks_InDevelopment_LogsTheConfirmationLinkInsteadOfCallingTheProvider()
    {
        var logs = new CapturingLoggerProvider();
        using var factory = new ApiWebApplicationFactory(database.ConnectionString, "Development")
        {
            AdditionalSettings = Settings(logLinks: "true"),
            ConfigureServices = services => services.AddSingleton<ILoggerProvider>(logs)
        };
        var email = $"dev-email-{Guid.NewGuid():N}@trainer.test";
        var client = factory.CreateOriginClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/signup",
            new { email, password = AuthTestData.Password, full_name = "Dev Email" },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Empty(factory.EmailRequests.Requests);
        var entry = Assert.Single(logs.Snapshot(), log =>
            log.FormattedMessage.Contains("confirmar-email?token=", StringComparison.Ordinal));
        Assert.Contains(email, entry.FormattedMessage, StringComparison.Ordinal);
        Assert.Contains("https://frontend.test/confirmar-email", entry.FormattedMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LogLinksOff_InDevelopment_StillSendsThroughTheProvider()
    {
        using var factory = new ApiWebApplicationFactory(database.ConnectionString, "Development");
        var client = factory.CreateOriginClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/signup",
            new
            {
                email = $"dev-email-off-{Guid.NewGuid():N}@trainer.test",
                password = AuthTestData.Password,
                full_name = "Dev Email"
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Single(factory.EmailRequests.Requests);
    }

    /// <summary>
    /// Production exige proxies conhecidos para os headers reencaminhados, por isso as
    /// definições incluem um: o que se prova aqui é só a opção de email.
    /// </summary>
    private static Dictionary<string, string> Settings(string? logLinks)
    {
        var settings = new Dictionary<string, string> { ["ForwardedHeaders:KnownProxies:0"] = "127.0.0.1" };
        if (logLinks is not null)
            settings[LogLinksSetting] = logLinks;
        return settings;
    }

    private static string Flatten(Exception exception)
    {
        var messages = new List<string>();
        for (var current = exception; current is not null; current = current.InnerException!)
            messages.Add(current.Message);
        return string.Join(" | ", messages);
    }
}
