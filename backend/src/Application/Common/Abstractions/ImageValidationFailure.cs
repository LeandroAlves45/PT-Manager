namespace Application.Common.Abstractions;

/// <summary>
/// Motivo pelo qual um conteúdo não é uma imagem publicavél, Conteúdo inválido é
/// um resultado esperado, não uma exceção.
/// </summary>
public enum ImageValidationFailure
{
    Empty,
    TooLarge,
    UnsupportedFormat,
    ContentTypeMismatch,
    NotDecodable,
    DimensionsTooSmall,
    DimensionsTooLarge,
    PixelBudgetExceeded,
    EncodingFailed
}
