namespace Application.Features.Training.ExerciseVideos;

/// <summary>Catálogo onde a escrita de um vídeo gerido é autorizada.</summary>
public enum ExerciseVideoCatalog
{
    Private,
    Global
}

/// <summary>Ator e rota a partir dos quais uma URL de reprodução é pedida.</summary>
public enum ExerciseVideoPlaybackAudience
{
    Trainer,
    GlobalCatalog,
    Administrative,
    Client
}
