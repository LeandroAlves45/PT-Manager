using Application.Features.Training.Exercises.Dtos;
using Application.Features.Training.Exercises.ListExercises;
using Application.Pagination;

namespace Application.Features.Training.Exercises.Abstractions;

/// <summary>Executa leituras projetadas do catálogo de exercícios.</summary>
public interface IExerciseQueries
{
    /// <summary>Obtém um exercício pelo seu ID.</summary>
    Task<ExerciseDto?> GetAsync(
        Guid exerciseId,
        CancellationToken cancellationToken
    );

    /// <summary>Lista exercícios privados para um personal trainer.</summary>
    Task<PageResult<ExerciseDto>> ListAsync(
        string? search,
        ExerciseActivityFilter activity,
        PageRequest page,
        CancellationToken cancellationToken
    );
}
