using Application.Features.Training.Exercises.Dtos;
using Application.Features.Training.Exercises.ListGlobalExercises;
using Application.Pagination;

namespace Application.Features.Training.Exercises.Abstractions;

/// <summary>Consulta exclusivamente exercícios globais para administração.</summary>
public interface IGlobalExerciseQueries
{
    Task<GlobalExerciseDto?> GetAsync(Guid exerciseId, CancellationToken cancellationToken);

    Task<PageResult<GlobalExerciseDto>> ListAsync(
        string? search,
        GlobalExerciseActivityFilter activity,
        PageRequest page,
        CancellationToken cancellationToken
    );
}
