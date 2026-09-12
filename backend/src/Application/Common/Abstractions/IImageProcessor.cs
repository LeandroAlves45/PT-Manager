namespace Application.Common.Abstractions;

/// <summary>Porta de descodificação, validação e normalização de imagens.</summary>
public interface IImageProcessor
{
    /// <summary>
    /// Lê o upload exatamente uma vez, prova que é uma imagem real, aplica os
    /// limites do perfil e devolve bytes reencodados sem metadados do original.
    /// </summary>
    Task<ImageProcessingResult> NormalizeAsync(
        MediaUpload upload,
        ImageProfile profile,
        CancellationToken cancellationToken);
}
