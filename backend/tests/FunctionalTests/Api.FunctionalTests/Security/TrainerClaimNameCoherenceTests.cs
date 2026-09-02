using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using Api.Authorization;
using Api.FunctionalTests.Support;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Api.FunctionalTests.Security;

/// <summary>
/// Fecha o gate QG4A-JWT-001 provando que o nome da claim de tenant é o mesmo dos
/// dois lados da fronteira, e que o nome anterior seria rejeitado.
/// </summary>
/// <remarks>
/// <see cref="JwtAuthenticationTests.ProductionIssuerToken_IsAcceptedByTheHttpPipeline"/>
/// prova que o emissor real é aceite. Esse teste sozinho não distingue, porém, entre
/// "o nome está correto" e "o leitor aceita qualquer nome". Os testes deste ficheiro
/// fecham essa lacuna por dois lados: a constante do emissor é lida por reflexão e
/// comparada com a da fronteira, e um token com o nome antigo <c>trainerId</c> é
/// rejeitado pelo pipeline. Sem isto, reintroduzir o defeito original manteria a suite
/// verde.
/// </remarks>
public sealed class TrainerClaimNameCoherenceTests : IDisposable
{
    private const string UnusedConnectionString =
        "Host=localhost;Port=5432;Database=unused;Username=unused;Password=unused";

    /// <summary>Nome incorreto que o emissor usava antes da correção do sub-lote 4A.</summary>
    private const string PreviousIssuerClaimName = "trainerId";

    private readonly ApiWebApplicationFactory _factory =
        new(UnusedConnectionString, "Testing");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public void IssuerAndHttpBoundary_DeclareTheSameTenantClaimName()
    {
        // A constante vive no assembly Infrastructure, que é onde o emissor real está;
        // ancorar noutro assembly resolveria um tipo inexistente.
        var issuerClaimNames = typeof(Infrastructure.Data.PtManagerDbContext)
            .Assembly
            .GetType("Infrastructure.Identity.JwtAccessTokenIssuer+ClaimNames", throwOnError: true)!;

        var issuedName = (string)issuerClaimNames
            .GetField("TrainerId", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetRawConstantValue()!;

        Assert.Equal(ApiClaimNames.TrainerId, issuedName);
        Assert.NotEqual(PreviousIssuerClaimName, issuedName);
    }

    [Fact]
    public async Task TokenCarryingThePreviousClaimName_IsRejectedByThePipeline()
    {
        var trainerId = Guid.NewGuid();
        var token = IssueWithTenantClaimName(
            trainerId,
            PreviousIssuerClaimName,
            trainerId);
        var client = _factory.CreateOriginClient().WithBearer(token);

        var response = await PostInviteClientAsync(client);

        // O middleware de tenant não encontra "trainer_id" e recusa o principal.
        // É este o 401 que todo o pedido autenticado de trainer sofria em produção.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TokenCarryingTheCanonicalClaimName_IsAcceptedByThePipeline()
    {
        var trainerId = Guid.NewGuid();
        var token = IssueWithTenantClaimName(
            trainerId,
            ApiClaimNames.TrainerId,
            trainerId);
        var client = _factory.CreateOriginClient().WithBearer(token);

        var response = await PostInviteClientAsync(client);

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    public void Dispose() => _factory.Dispose();

    /// <summary>
    /// Emite um token idêntico ao de produção, mas com o nome da claim de tenant
    /// escolhido pelo teste, para que o nome seja a única variável em prova.
    /// </summary>
    private static string IssueWithTenantClaimName(
        Guid userId,
        string tenantClaimName,
        Guid trainerId)
    {
        var issuedAt = DateTime.UtcNow;

        var descriptor = new Microsoft.IdentityModel.Tokens.SecurityTokenDescriptor
        {
            Issuer = ApiWebApplicationFactory.Issuer,
            Audience = ApiWebApplicationFactory.Audience,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(ApiClaimNames.Role, ApiRoleNames.Trainer),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
                new Claim(tenantClaimName, trainerId.ToString())
            ]),
            IssuedAt = issuedAt,
            NotBefore = issuedAt,
            Expires = issuedAt.AddMinutes(15),
            SigningCredentials = new Microsoft.IdentityModel.Tokens.SigningCredentials(
                new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                    Convert.FromBase64String(ApiWebApplicationFactory.JwtSigningMaterial)),
                Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256)
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    private static Task<HttpResponseMessage> PostInviteClientAsync(HttpClient client) =>
        client.PostAsJsonAsync(
            "/api/v1/auth/invite-client",
            new { client_id = Guid.Empty, email = string.Empty },
            Token);
}
