using System.Security.Claims;
using Application.Common.Abstractions;
using Application.Features.Authentication.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure.Identity;

/// <summary>Assina access tokens HS256 a partir de uma identidade já validada.</summary>
internal sealed class JwtAccessTokenIssuer : IAccessTokenIssuer
{
    private readonly JwtOptions _options;
    private readonly IClock _clock;
    private readonly SigningCredentials _credentials;

    public JwtAccessTokenIssuer(
        IOptions<JwtOptions> options,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _credentials = new SigningCredentials(
            new SymmetricSecurityKey(_options.GetSigningKeyBytes()),
            SecurityAlgorithms.HmacSha256);
    }

    public IssuedAccessToken Issue(AuthenticatedPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        var issuedAt = _clock.UtcNow;
        var expiresAt = issuedAt.Add(_options.Lifetime);

        var claims = new List<Claim>(6)
        {
            new(JwtRegisteredClaimNames.Sub, principal.UserId.ToString()),
            new(ClaimNames.Role, principal.Role),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };

        // O superuser não pertence a nenhum tenant; a claim é omitida em vez de
        // enviada vazia, para que a sua ausência seja um fato verificável.
        if (principal.TrainerId.HasValue)
            claims.Add(new Claim(ClaimNames.TrainerId, principal.TrainerId.Value.ToString()));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = issuedAt,
            NotBefore = issuedAt,
            Expires = expiresAt,
            SigningCredentials = _credentials
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);
        return new IssuedAccessToken(token, expiresAt);
    }

    /// <summary>Nomes de claim partilhados com a fronteira HTTP.</summary>
    internal static class ClaimNames
    {
        internal const string Role = "role";
        internal const string TrainerId = "trainerId";
    }
}
