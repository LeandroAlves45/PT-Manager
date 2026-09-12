namespace Application.Common.Abstractions;

/// <summary>Perfis de imagem suportados pelo sistema.</summary>
public static class ImageProfiles
{
    /// <summary>
    /// Logo do personal trainer. Não é moderado: o personal trainer é um ator identificado,
    /// com subscrição e contrato, e responde pela sua própria marca. Mantém todas
    /// as restantes defesas — descodificação real, limites e remoção de metadados.
    /// </summary>
    public static readonly ImageProfile TrainerLogo = new(
        Name: "trainer_logo",
        MaxBytes: 5L * 1024 * 1024,
        MinDimension: 64,
        MaxDimension: 6000,
        MaxPixels: 30_000_000,
        OutputMaxDimension: 512,
        OutputContentType: "image/webp",
        OutputQuality: 90,
        RequiresModeration: false);

    /// <summary>
    /// Avatar do cliente. É moderado de forma síncrona e fail-closed: o contéudo
    /// é submetido por um ator que o personal trainer não controla.
    /// </summary>
    public static readonly ImageProfile ClientAvatar = new(
        Name: "client_avatar",
        MaxBytes: 4L * 1024 * 1024,
        MinDimension: 64,
        MaxDimension: 6000,
        MaxPixels: 30_000_000,
        OutputMaxDimension: 512,
        OutputContentType: "image/webp",
        OutputQuality: 82,
        RequiresModeration: true);

    /// <summary>Content-Types aceites na fronteira, antes de qualquer descodificação.</summary>
    public static readonly IReadOnlySet<string> AcceptedContentTypes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "image/png",
            "image/jpeg",
            "image/webp"
        };
}
