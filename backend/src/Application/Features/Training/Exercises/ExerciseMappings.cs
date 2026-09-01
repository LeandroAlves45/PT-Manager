using Application.Features.Training.Exercises.Dtos;
using Domain.Entities.Training;

namespace Application.Features.Training.Exercises;

/// <summary>Converte Exercise em contratos de leitura da Application.</summary>
public static class ExerciseMappings
{
    /// <summary>Mapeia a entidade sem expor o identificador do tenant.</summary>
    public static ExerciseDto ToDto(this Exercise exercise)
    {
        ArgumentNullException.ThrowIfNull(exercise);

        return new ExerciseDto(
            exercise.Id,
            exercise.OwnerTrainerId is null ? "global" : "private",
            exercise.Name,
            exercise.Description,
            exercise.MuscleGroups,
            exercise.Equipment,
            exercise.DifficultyLevel,
            exercise.VideoUrl,
            exercise.IsActive,
            exercise.PlatformEnforcementStatus.Value,
            exercise.PlatformEnforcementReason?.Value,
            exercise.CreatedAt,
            exercise.UpdatedAt
        );
    }
}
