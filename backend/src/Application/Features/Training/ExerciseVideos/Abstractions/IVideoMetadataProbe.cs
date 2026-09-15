namespace Application.Features.Training.ExerciseVideos.Abstractions;

/// <summary>
/// Lê a estrutura real do container armazenado sem descarregar o ficheiro
/// inteiro e sem confiar no tipo declarado.
///
/// </summary>
public interface IVideoMetadataProbe
{
    /// <summary>
    /// Extrai container, codecs, duração e resolução. ETag confirmado
    /// na finalização garante que o objeto lido é o objeto aceite.
    /// </summary>
    Task<VideoProbeOutcome> ProbeAsync(
        string objectKey,
        Guid? ownerTrainerId,
        long sizeBytes,
        string eTag,
        CancellationToken cancellationToken);
}
