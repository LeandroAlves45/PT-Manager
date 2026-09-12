using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace Infrastructure.Media.Cloudinary;

/// <summary>Falha o arranque quando o storage ativo não tem configuração segura.</summary>
internal sealed partial class CloudinaryOptionsValidator : IValidateOptions<CloudinaryOptions>
{
    public ValidateOptionsResult Validate(string? name, CloudinaryOptions options)
    {
        var failures = new List<string>();

        if (options.Timeout <= TimeSpan.Zero || options.Timeout > TimeSpan.FromMinutes(1))
            failures.Add("Cloudinary timeout is outside the allowed range");

        if (options.BaseAddress is null || !options.BaseAddress.IsAbsoluteUri ||
            options.BaseAddress.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(options.BaseAddress.UserInfo) ||
            !string.IsNullOrEmpty(options.BaseAddress.Fragment))
            failures.Add(
                "Cloudinary base address must be an absolute HTTPS URL without user-info or fragment");

        // A raiz entra na assinatura e no prefixo que o adapter aceita eliminar:
        // tem de ser um caminho fechado, sem '..', espaços ou barras soltas.
        if (string.IsNullOrWhiteSpace(options.FolderRoot) ||
            !FolderRootPattern().IsMatch(options.FolderRoot))
            failures.Add("Cloudinary folder root is invalid");

        if (options.Enabled)
        {
            if (string.IsNullOrWhiteSpace(options.CloudName) ||
                !CloudNamePattern().IsMatch(options.CloudName))
                failures.Add("Cloudinary cloud name is required and must be lowercase alphanumeric");

            if (string.IsNullOrWhiteSpace(options.ApiKey))
                failures.Add("Cloudinary API key is required");

            if (string.IsNullOrWhiteSpace(options.ApiSecret))
                failures.Add("Cloudinary API secret is required");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9_-]{0,63}(/[a-z0-9][a-z0-9_-]{0,63}){0,3}$", RegexOptions.CultureInvariant)]
    private static partial Regex FolderRootPattern();

    [GeneratedRegex("^[a-z0-9][a-z0-9_-]{1,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex CloudNamePattern();
}
