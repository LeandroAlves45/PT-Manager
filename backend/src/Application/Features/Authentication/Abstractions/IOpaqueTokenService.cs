namespace Application.Features.Authentication.Abstractions;

/// <summary>
/// Define a geração e o hashing de tokens opacos sem acoplar a Application ás
/// primitivas criptográficas concretas da Infraestruture.
/// </summary>
public interface IOpaqueTokenService
{
    /// <summary>Gera um token com entropia criptográfica e o respetivo hash.</summary>
    GeneratedOpaqueToken Generate();

    /// <summary>Calcula o hash no mesmo formato usando persistência.</summary>
    string Hash(string rawToken);
}
