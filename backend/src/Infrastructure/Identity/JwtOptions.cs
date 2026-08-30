using System.Text;

namespace Infrastructure.Identity;

/// <summary>Configuração validada da emissão de access tokens.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string SigningKey { get; set; } = string.Empty;
    public TimeSpan Lifetime { get; set; } = TimeSpan.FromMinutes(15);
    public TimeSpan ClockSkew { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Devolve os bytes da chave depois de garantir que tem entropia suficiente.</summary>
    public byte[] GetSigningKeyBytes()
    {
        byte[] key;

        try
        {
            key = Convert.FromBase64String(SigningKey);
        }
        catch (FormatException)
        {
            // Aceita também uma chave de texto para ambientes de desenvolvimento,
            // desde que tenha o mesmo número de bytes reais.
            key = Encoding.UTF8.GetBytes(SigningKey);
        }

        if (key.Length < 32)
            throw new InvalidOperationException(
                "Configuration 'Jwt:SigningKey' must provide at least 256 bits of key material.");

        return key;
    }

    /// <summary>Valida a secção inteira no arranque da aplicação.</summary>
    public bool IsValid()
    {
        if (string.IsNullOrWhiteSpace(Issuer) ||
            string.IsNullOrWhiteSpace(Audience) ||
            string.IsNullOrWhiteSpace(SigningKey))
            return false;

        if (Lifetime <= TimeSpan.Zero || Lifetime > TimeSpan.FromMinutes(15))
            return false;

        if (ClockSkew <= TimeSpan.Zero || ClockSkew > TimeSpan.FromSeconds(30))
            return false;

        try
        {
            GetSigningKeyBytes();
        }
        catch (InvalidOperationException)
        {
            return false;
        }

        return true;
    }
}
