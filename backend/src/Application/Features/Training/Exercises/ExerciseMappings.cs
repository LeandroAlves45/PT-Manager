using Application.Features.Training.Exercises.Dtos;
using Domain.Entities.Training;

namespace Application.Features.Training.Exercises;

/// <summary>Converte Exercise em contratos de leitura da Application.</summary>
public static class ExerciseMappings
{
    /// <summary>Mapeia a entidade sem expor o identificador do tenant.</summary>
    /// <remarks>
    /// Sem acesso aos vídeos: usar só onde o exercício acabou de nascer (criação). Leituras e a
    /// resposta de uma edição passam por <c>IExerciseQueries</c>, que calcula o estado do vídeo.
    /// </remarks>
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
            null,
            false,
            exercise.IsActive,
            exercise.PlatformEnforcementStatus.Value,
            exercise.PlatformEnforcementReason?.Value,
            exercise.CreatedAt,
            exercise.UpdatedAt
        );
    }
}
