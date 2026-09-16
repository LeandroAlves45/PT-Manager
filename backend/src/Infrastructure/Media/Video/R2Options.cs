using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace Infrastructure.Media.Video;

/// <summary>Configuração do storage privado de vídeos em Cloudflare R2.</summary>
public sealed class R2Options
{
    public const string SectionName = "R2";

    public bool Enabled { get; init; }

    public string? AccountId { get; init; }

    public string? AccessKeyId { get; init; }

    public string? SecretAccessKey { get; init; }

    public string? BucketName { get; init; }

    /// <summary>Prefico de todos os objetos geridos.</summary>
    public string KeyPrefix { get; init; } = "pt-manager";

    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(20);

    /// <summary>
    /// Endpoint S3 explícito. Por omissão é derivado do account ID; só existe para
    /// endpoints S3-compatíveis de teste.
    /// </summary>
    public Uri? ServiceUrl { get; init; }

    internal Uri ResolveServiceUrl() =>
        ServiceUrl ?? new Uri($"https://{AccountId}.r2.cloudflarestorage.com");
}

/// <summary>Falha o arranque quando o storage ativo não tem configuração segura.</summary>
internal sealed partial class R2OptionsValidator : IValidateOptions<R2Options>
{
    public ValidateOptionsResult Validate(string? name, R2Options options)
    {
        var failures = new List<string>();

        if (options.Timeout <= TimeSpan.Zero || options.Timeout > TimeSpan.FromMinutes(1))
            failures.Add("R2 timeout is outside the allowed range.");

        // O prefixo entra em todos os identificadores e delimita o que o adapter
        // aceita ler ou eliminar: tem de ser um caminho fechado.
        if (string.IsNullOrWhiteSpace(options.KeyPrefix) || !KeyPrefixPattern().IsMatch(options.KeyPrefix))
            failures.Add("R2 key prefix is invalid.");

        if (options.ServiceUrl is not null &&
            (!options.ServiceUrl.IsAbsoluteUri ||
                options.ServiceUrl.Scheme != Uri.UriSchemeHttps ||
                !string.IsNullOrEmpty(options.ServiceUrl.UserInfo) ||
                !string.IsNullOrEmpty(options.ServiceUrl.Fragment)))
            failures.Add("R2 service URL must be an absolute HTTPS URL without user-info or fragment.");

        if (options.Enabled)
        {
            if (string.IsNullOrWhiteSpace(options.AccountId) ||
                !AccountIdPattern().IsMatch(options.AccountId))
                failures.Add(
                    "R2 account ID is required and must be 32 lowercase hexadecimal characters.");

            if (string.IsNullOrWhiteSpace(options.AccessKeyId))
                failures.Add("R2 access key ID is required.");

            if (string.IsNullOrWhiteSpace(options.SecretAccessKey))
                failures.Add("R2 secret access key is required.");

            if (string.IsNullOrWhiteSpace(options.BucketName) ||
                !BucketNamePattern().IsMatch(options.BucketName))
                failures.Add("R2 bucket name is required and must be a valid bucket name.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9_-]{0,63}(/[a-z0-9][a-z0-9_-]{0,63}){0,3}$", RegexOptions.CultureInvariant)]
    private static partial Regex KeyPrefixPattern();

    [GeneratedRegex("^[a-f0-9]{32}$", RegexOptions.CultureInvariant)]
    private static partial Regex AccountIdPattern();

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{1,61}[a-z0-9]$", RegexOptions.CultureInvariant)]
    private static partial Regex BucketNamePattern();
}
