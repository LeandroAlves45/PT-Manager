using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Infrastructure.Media.Moderation;

/// <summary>Falha o arranque quando a moderação ativa não tem configuração segura.</summary>
/// <remarks>
/// Valida apenas a forma do JSON da service account, sem I/O de rede, e nunca
/// inclui o seu conteúdo nas mensagens de falha: estas mensagens chegam aos logs
/// de arranque.
/// </remarks>
internal sealed class VisionOptionsValidator : IValidateOptions<VisionOptions>
{
    public ValidateOptionsResult Validate(string? name, VisionOptions options)
    {
        var failures = new List<string>();

        // O timeout vive dentro de um pedido HTTP síncrono do utilizador: um valor
        // alto prende uma ligação e um thread por cada upload de avatar.
        if (options.Timeout <= TimeSpan.Zero || options.Timeout > TimeSpan.FromSeconds(30))
            failures.Add("Vision timeout is outside the allowed range.");

        if (options.BaseAddress is null || !options.BaseAddress.IsAbsoluteUri ||
            options.BaseAddress.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(options.BaseAddress.UserInfo) ||
            !string.IsNullOrEmpty(options.BaseAddress.Fragment))
            failures.Add(
                "Vision base address must be an absolute HTTPS URL without user-info or fragment.");

        if (options.Enabled && !IsServiceAccountJson(options.ServiceAccountJson))
            failures.Add("Vision service account JSON is required and must describe a service account.");

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static bool IsServiceAccountJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            return root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("type", out var type) &&
                type.ValueKind == JsonValueKind.String &&
                type.GetString() == "service_account" &&
                HasText(root, "client_email") &&
                HasText(root, "private_key");
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool HasText(JsonElement root, string property) =>
        root.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.String &&
        !string.IsNullOrWhiteSpace(value.GetString());
}
