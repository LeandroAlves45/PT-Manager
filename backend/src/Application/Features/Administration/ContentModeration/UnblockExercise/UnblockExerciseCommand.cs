namespace Application.Features.Administration.ContentModeration.UnblockExercise;

/// <summary>Solicita a remoção do bloqueio administrativo de um exercício privado.</summary>
public sealed record UnblockExerciseCommand(Guid ExerciseId);
