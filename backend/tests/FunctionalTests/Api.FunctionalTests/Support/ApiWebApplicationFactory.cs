using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Api.FunctionalTests.Support;

/// <summary>Cria a API com configuração isolada e PostgreSQL controlado pelo teste.</summary>
public sealed class ApiWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public ApiWebApplicationFactory(string connectionString) =>
        _connectionString = connectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // UseSetting escreve na host configuration, lida antes de Program registar
        // serviços. ConfigureAppConfiguration chegaria tarde de mais no minimal hosting.
        builder.UseSetting("ConnectionStrings:DefaultConnection", _connectionString);
        builder.UseSetting("Cors:AllowedOrigins:0", "https://frontend.test");

        // Sem porta HTTPS conhecida, UseHttpsRedirection nao consegue redirecionar.
        builder.UseSetting("https_port", "443");
    }
}
