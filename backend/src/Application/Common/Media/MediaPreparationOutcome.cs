using Application.Common.Abstractions;

namespace Application.Common.Media;

/// <summary>Resultado da preparação de uma imagem.</summary>
public sealed record MediaPreparationOutcome(
    MediaPreparationStatus Status,
    StoredMedia? Media = null,
    ImageValidationFailure? ValidationFailure = null)
{
    /// <summary>Contéudo publicado e pronto a ser referenciado.</summary>
    public static MediaPreparationOutcome Prepared(StoredMedia media) =>
        new(MediaPreparationStatus.Prepared, media);

    /// <summary>Conteúdo rejeitado na validação de imagem.</summary>
    public static MediaPreparationOutcome InvalidImage(ImageValidationFailure failure) =>
        new(MediaPreparationStatus.InvalidImage, ValidationFailure: failure);

    /// <summary>Estado terminal sem asset publicado.</summary>
    public static MediaPreparationOutcome From(MediaPreparationStatus status) =>
        new(status);
}
