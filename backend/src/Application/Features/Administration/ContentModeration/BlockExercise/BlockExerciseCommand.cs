namespace Application.Features.Administration.ContentModeration.BlockExercise;

/// <summary>Solicita o bloqueio de um exercício privado com motivo estruturado.</summary>
public sealed record BlockExerciseCommand(
    Guid ExerciseId,
    string ReasonCode);
