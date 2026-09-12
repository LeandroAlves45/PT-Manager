using Application.Common.Abstractions;

namespace Api.Http;

/// <summary>
/// Converte um ficheiro multipart recebido na fronteira HTTP no contrato MediaUpload.
/// </summary>
internal static class FormFileMediaUpload
{
    /// <summary>
    /// Tecto do corpo HTTP: o maior limite de perfil (5 MiB) mais folga para as
    /// fronteiras e cabeçalhos multipart. Um único valor para todas as rotas de
    /// imagem evita divergência silenciosa entre elas. O limite real por tipo
    /// de imagem é aplicado depois, sobre os bytes efetivamente lidos.
    /// </summary>
    internal const long MaxRequestBytes = 6 * 1024 * 1024;
    internal const int MaxPartHeadersBytes = 8 * 1024;
    internal const int MaxFormValues = 4;

    internal static MediaUpload? From(IFormFile? file) =>
        file is null
            ? null
            : new MediaUpload(file.OpenReadStream(), file.ContentType ?? string.Empty, file.Length);
}
