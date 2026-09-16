namespace Application.Features.Training.ExerciseVideos;

/// <summary>
/// Parâmetros operacionais configuráveis dos vídeos geridos. A Infrastructure
/// constrói este valor a partir da configuração validada no arranque.
/// </summary>
public sealed record ExerciseVideoSettings
{
    public ExerciseVideoSettings(
        int maxVideosPerTrainer,
        TimeSpan uploadUrlLifetime,
        TimeSpan playbackUrlLifetime,
        TimeSpan abandonmentGrace)
    {
        if (maxVideosPerTrainer is < 1 or > 1000)
            throw new ArgumentOutOfRangeException(nameof(maxVideosPerTrainer));
        if (uploadUrlLifetime < TimeSpan.FromMinutes(1) ||
            uploadUrlLifetime > TimeSpan.FromHours(1))
            throw new ArgumentOutOfRangeException(nameof(uploadUrlLifetime));
        if (playbackUrlLifetime < TimeSpan.FromMinutes(1) ||
            playbackUrlLifetime > TimeSpan.FromHours(12))
            throw new ArgumentOutOfRangeException(nameof(playbackUrlLifetime));

        // O retry do dispatcher (5 tentativas, 1+2+4+8 min) cabe em ~15 min.
        // A margem mínima de 1 hora cobre esse orçamento e o jitter.
        if (abandonmentGrace < TimeSpan.FromHours(1) || abandonmentGrace > TimeSpan.FromDays(1))
            throw new ArgumentOutOfRangeException(nameof(abandonmentGrace));

        MaxVideosPerTrainer = maxVideosPerTrainer;
        UploadUrlLifetime = uploadUrlLifetime;
        PlaybackUrlLifetime = playbackUrlLifetime;
        AbandonmentGrace = abandonmentGrace;
    }

    public int MaxVideosPerTrainer { get; }
    public TimeSpan UploadUrlLifetime { get; }
    public TimeSpan PlaybackUrlLifetime { get; }

    /// <summary>
    /// Margem entre o fim da janela de upload e a limpeza de um vídeo que não
    /// terminou. Tem de exceder o orçamento de retry do processamento.
    /// </summary>
    public TimeSpan AbandonmentGrace { get; }
}
