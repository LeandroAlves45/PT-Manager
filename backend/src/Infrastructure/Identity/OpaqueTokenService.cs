using System.Security.Cryptography;
using Application.Features.Authentication.Abstractions;

namespace Infrastructure.Identity;

/// <summary>Implementação criptográfica dos tokens opacos locais.</summary>
internal sealed class OpaqueTokenService : IOpaqueTokenService
{
    public GeneratedOpaqueToken Generate()
    {
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        return new GeneratedOpaqueToken(rawToken, Hash(rawToken));
    }

    public string Hash(string rawToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawToken);
        return Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken)));
    }
}
