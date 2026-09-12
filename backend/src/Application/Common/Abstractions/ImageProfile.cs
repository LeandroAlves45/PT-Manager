namespace Application.Common.Abstractions;

/// <summary>
/// Política de produto para um tipo de imagem: limites aceites, formato de saída
/// e se o conteúdo carece de moderação.
/// </summary>
public sealed record ImageProfile(
    string Name,
    long MaxBytes,
    int MinDimension,
    int MaxDimension,
    long MaxPixels,
    int OutputMaxDimension,
    string OutputContentType,
    int OutputQuality,
    bool RequiresModeration);
