namespace Application.Common.Abstractions;

/// <summary>
/// Imagem já normalizada: reencodada a partir dos píxeis descodificados e, por
/// isso, sem EXIF, sem geolocalização, sem ICC e sem XMP do ficheiro original.
/// </summary>
public sealed record ProcessedImage(
    ReadOnlyMemory<byte> Content,
    string ContentType,
    int Width,
    int Height);

/// <summary>Resultado da normalização. Extamente uma das propriedades é não nula.</summary>
public sealed record ImageProcessingResult(
    ProcessedImage? Image,
    ImageValidationFailure? Failure)
{
    /// <summary>Normalização bem sucedida.</summary>
    public static ImageProcessingResult Ok(ProcessedImage image) => new(image, null);

    /// <summary>Contéudo recusado, com motivo estável.</summary>
    public static ImageProcessingResult Invalid(ImageValidationFailure failure) =>
        new(null, failure);
}
