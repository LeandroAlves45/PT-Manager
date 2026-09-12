using System.Security.Cryptography;
using System.Text;

namespace Infrastructure.Media.Cloudinary;

/// <summary>Calcula a assinatura dos pedidos á Upload API do Cloudinary.</summary>
internal static class CloudinarySignature
{
    private static readonly HashSet<string> ExcludedParameters = new(StringComparer.Ordinal)
    {
        "file",
        "cloud_name",
        "resource_type",
        "api_key",
        "signature"
    };

    /// <summary>Assina o conjunto de parâmetros com o segredo indicado.</summary>
    internal static string Sign(IReadOnlyDictionary<string, string> parameters, string apiSecret)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiSecret);

        var serialized = string.Join(
            '&',
            parameters
                .Where(parameter =>
                    !ExcludedParameters.Contains(parameter.Key) &&
                    !string.IsNullOrEmpty(parameter.Value))
                .OrderBy(parameter => parameter.Key, StringComparer.Ordinal)
                .Select(parameter => $"{parameter.Key}={parameter.Value}"));

#pragma warning disable CA5350 // SHA-1 é imposto pelo protocolo de assinatura do fornecedor.
        var hash = SHA1.HashData(Encoding.UTF8.GetBytes(serialized + apiSecret));
#pragma warning restore CA5350

        return Convert.ToHexStringLower(hash);
    }
}
