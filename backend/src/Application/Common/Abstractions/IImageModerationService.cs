namespace Application.Common.Abstractions;

/// <summary>Porta de moderação síncrona de conteúdos visuais.</summary>
public interface IImageModerationService
{
    /// <summary>
    /// Avalia o conteúdo e devolve um veredicto.
    /// </summary>
    Task<ImageModerationResult> ReviewAsync(
        ImageModerationRequest request,
        CancellationToken cancellationToken);
}
