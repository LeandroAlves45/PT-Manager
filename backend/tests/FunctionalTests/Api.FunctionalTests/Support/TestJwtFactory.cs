using System.Security.Claims;
using Api.Authorization;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Api.FunctionalTests.Support;

/// <summary>
/// Emite access tokens válidos para os testes funcionais, com o mesmo formato do
/// emissor de produção.
/// </summary>
internal static class TestJwtFactory
{
    internal static string IssueTrainer(Guid trainerUserId) =>
        Issue(trainerUserId, ApiRoleNames.Trainer, trainerUserId);

    internal static string IssueClient(Guid clientUserId, Guid trainerId) =>
        Issue(clientUserId, ApiRoleNames.Client, trainerId);

    internal static string IssueSuperuser(Guid userId) =>
        Issue(userId, ApiRoleNames.Superuser, trainerId: null);

    internal static string Issue(
        Guid userId,
        string role,
        Guid? trainerId,
        DateTime? expiresAt = null)
    {
        var issuedAt = DateTime.UtcNow;

        var claims = new List<Claim>(4)
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(ApiClaimNames.Role, role),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };

        if (trainerId.HasValue)
            claims.Add(new Claim(ApiClaimNames.TrainerId, trainerId.Value.ToString()));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = ApiWebApplicationFactory.Issuer,
            Audience = ApiWebApplicationFactory.Audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = issuedAt,
            NotBefore = issuedAt,
            Expires = expiresAt ?? issuedAt.AddMinutes(15),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(
                    Convert.FromBase64String(ApiWebApplicationFactory.JwtSigningMaterial)),
                SecurityAlgorithms.HmacSha256)
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    internal static string IssueWithSigningKey(
        Guid userId,
        string role,
        Guid? trainerId,
        string signingMaterialBase64)
    {
        var issuedAt = DateTime.UtcNow;

        var claims = new List<Claim>(4)
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(ApiClaimNames.Role, role),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };

        if (trainerId.HasValue)
            claims.Add(new Claim(ApiClaimNames.TrainerId, trainerId.Value.ToString()));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = ApiWebApplicationFactory.Issuer,
            Audience = ApiWebApplicationFactory.Audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = issuedAt,
            NotBefore = issuedAt,
            Expires = issuedAt.AddMinutes(15),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Convert.FromBase64String(signingMaterialBase64)),
                SecurityAlgorithms.HmacSha256)
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
