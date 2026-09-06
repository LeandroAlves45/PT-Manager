using System.Security.Cryptography;
using Application.Features.Authentication.Abstractions;
using Infrastructure.Data;
using Infrastructure.Data.Interceptors;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;

namespace Api.FunctionalTests.Support;

/// <summary>Cria a API com configuração isolada e PostgreSQL controlado pelo teste.</summary>
public sealed class ApiWebApplicationFactory : WebApplicationFactory<Program>
{
    /// <summary>
    /// Material de assinatura gerado no arranque do processo de teste.
    /// </summary>
    /// <remarks>
    /// Derivado em runtime e não escrito como literal. Um literal de 32
    /// bytes num ficheiro versionado é indistinguível de uma chave real para
    /// qualquer scanner, e o valor nem precisa de ser estável entre execuções —
    /// só entre o host e os testes do mesmo processo, que partilham este campo.
    /// </remarks>
    public static readonly string JwtSigningMaterial =
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    /// <summary>Emissor esperado nos tokens emitidos durante os testes.</summary>
    public const string Issuer = "https://api.test";

    /// <summary>Audiência esperada nos tokens emitidos durante os testes.</summary>
    public const string Audience = "ptmanager-tests";

    /// <summary>Origem única aceite pela allowlist de CORS e pelo filtro de Origin.</summary>
    public const string AllowedOrigin = "https://frontend.test";

    /// <summary>Credencial fictícia do adapter de email; nunca sai do processo.</summary>
    public const string EmailProviderCredential = "email-provider-double";

    /// <summary>
    /// Client ID Google fictício. É um identificador público por desenho, não um
    /// segredo: o que valida a credencial é a assinatura do ID token, verificada
    /// pelo adapter real ou pelo duplo instalado em <see cref="ConfigureServices"/>.
    /// </summary>
    public const string GoogleClientId = "ptmanager-tests.apps.googleusercontent.com";

    private readonly string _connectionString;
    private readonly string _environmentName;

    /// <summary>
    /// Registos adicionais aplicados depois dos serviços da API, para um teste
    /// substituir portas como o verificador Google sem contactar a Google.
    /// </summary>
    public Action<IServiceCollection>? ConfigureServices { get; init; }

    /// <summary>Grava os pedidos que o adapter de email teria enviado.</summary>
    public RecordingHttpMessageHandler EmailRequests { get; } = new();

    public ApiWebApplicationFactory(
        string connectionString,
        string environmentName = "Testing")
    {
        _connectionString = connectionString;
        _environmentName = environmentName;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(_environmentName);

        // UseSetting escreve na host configuration, lida antes de Program registar
        // serviços. ConfigureAppConfiguration chegaria tarde de mais no minimal hosting.
        builder.UseSetting("ConnectionStrings:DefaultConnection", _connectionString);
        builder.UseSetting("Cors:AllowedOrigins:0", AllowedOrigin);

        // Sem porta HTTPS conhecida, UseHttpsRedirection nao consegue redirecionar.
        builder.UseSetting("https_port", "443");

        // A API passa a exigir estas secções e falha no arranque
        // sem elas, por desenho. Os valores são de teste e não têm equivalente
        // em nenhum ambiente real.
        builder.UseSetting("Jwt:Issuer", Issuer);
        builder.UseSetting("Jwt:Audience", Audience);
        builder.UseSetting("Jwt:SigningKey", JwtSigningMaterial);
        builder.UseSetting("Jwt:ClockSkew", "00:00:30");

        builder.UseSetting("AuthCookies:SameSite", "Lax");

        // AddGoogleAuthenticationInfrastructure valida esta secção com ValidateOnStart:
        // sem ela nenhum host funcional arranca.
        builder.UseSetting("Google:ClientId", GoogleClientId);

        builder.UseSetting("Resend:ApiKey", EmailProviderCredential);
        builder.UseSetting("Resend:FromAddress", "no-reply@ptmanager.test");
        builder.UseSetting("Resend:FrontendBaseUrl", AllowedOrigin);
        builder.UseSetting("Resend:BaseAddress", "https://resend.test/");

        builder.ConfigureTestServices(services =>
        {
            // Substituir o handler primário, e não o IAuthenticationEmailSender,
            // mantém o adapter real em teste. É o único desenho que prova o URL,
            // os headers e o corpo que o fornecedor receberia — trocar o serviço
            // inteiro por um duplo deixaria esse código sem cobertura nenhuma.
            services.Configure<HttpClientFactoryOptions>(
                nameof(IAuthenticationEmailSender),
                options => options.HttpMessageHandlerBuilderActions.Add(
                    handlerBuilder => handlerBuilder.PrimaryHandler = EmailRequests));

            services.AddSingleton<CommandCountingInterceptor>();
            services.AddDbContext<PtManagerDbContext>((provider, options) =>
            {
                options.UseNpgsql(_connectionString, npgsql =>
                {
                    npgsql.MigrationsAssembly(typeof(PtManagerDbContext).Assembly.FullName);
                    npgsql.EnableRetryOnFailure(maxRetryCount: 3);
                });

                options.AddInterceptors(
                    provider.GetRequiredService<TenantWriteValidationInterceptor>(),
                    provider.GetRequiredService<CommandCountingInterceptor>());
            });

            ConfigureServices?.Invoke(services);
        });
    }

    /// <summary>Cria um cliente que envia sempre a origem aprovada.</summary>
    /// <remarks>
    /// Os endpoints de Auth exigem Origin. Um helper evita que cada
    /// teste repita o header e evita falsos negativos em que o teste falha por
    /// esquecimento e não pelo comportamento em prova.
    /// </remarks>
    public HttpClient CreateOriginClient()
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
            BaseAddress = new Uri("https://localhost")
        });
        client.DefaultRequestHeaders.Add("Origin", AllowedOrigin);
        return client;
    }
}
