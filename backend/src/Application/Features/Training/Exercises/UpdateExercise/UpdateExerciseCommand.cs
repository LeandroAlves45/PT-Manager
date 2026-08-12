namespace Application.Features.Training.Exercises.UpdateExercise;

/// <summary>Solicita a atualização integral dos campos editáveis de um exercício.</summary>
public sealed record UpdateExerciseCommand(
    Guid ExerciseId,
    string Name,
    string? Description,
    string? MuscleGroups,
    string? Equipment,
    string? DifficultyLevel,
    string? VideoUrl
);
