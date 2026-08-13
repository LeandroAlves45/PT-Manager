using Application.Features.Training.Exercises.Abstractions;
using Application.Features.Training.Exercises.Dtos;
using Application.Features.Training.Exercises.ListExercises;
using Application.Pagination;
using Domain.Entities.Training;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Infrastructure.Persistence.Training;

/// <summary>Executa queries paginadas de exercícios sem tracking.</summary>
internal sealed class ExerciseQueries : IExerciseQueries
{
    private const string EscapeCharacter = "\\";
    private readonly PtManagerDbContext _dbContext;

    public ExerciseQueries(PtManagerDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public Task<ExerciseDto?> GetAsync(
        Guid exerciseId,
        CancellationToken cancellationToken = default) =>
        VisibleQuery()
            .Where(exercise => exercise.Id == exerciseId)
            .Select(Projection)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<PageResult<ExerciseDto>> ListAsync(
        string? search,
        ExerciseActivityFilter activity,
        PageRequest page,
        CancellationToken cancellationToken = default)
    {
        var query = activity switch
        {
            ExerciseActivityFilter.Active => VisibleQuery()
                .Where(exercise => exercise.OwnerTrainerId == null || exercise.IsActive),
            ExerciseActivityFilter.Archived => VisibleQuery()
                .Where(exercise => exercise.OwnerTrainerId != null && !exercise.IsActive),
            ExerciseActivityFilter.All => VisibleQuery(),
            _ => throw new ArgumentOutOfRangeException(nameof(activity))
        };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{EscapeLike(search)}%";
            query = query.Where(exercise =>
                EF.Functions.ILike(exercise.Name, pattern, EscapeCharacter) ||
                exercise.Description != null &&
                    EF.Functions.ILike(exercise.Description, pattern, EscapeCharacter));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(exercise => exercise.Name)
            .ThenBy(exercise => exercise.OwnerTrainerId == null)
            .ThenBy(exercise => exercise.Id)
            .Skip((page.PageNumber - 1) * page.PageSize)
            .Take(page.PageSize)
            .Select(Projection)
            .ToListAsync(cancellationToken);

        return new PageResult<ExerciseDto>(items, totalCount);
    }

    private IQueryable<Exercise> VisibleQuery() =>
        _dbContext.Exercises
            .AsNoTracking()
            .Where(exercise => exercise.OwnerTrainerId != null || exercise.IsActive);

    private static Expression<Func<Exercise, ExerciseDto>> Projection =>
        exercise => new ExerciseDto(
            exercise.Id,
            exercise.OwnerTrainerId == null ? "global" : "private",
            exercise.Name,
            exercise.Description,
            exercise.MuscleGroups,
            exercise.Equipment,
            exercise.DifficultyLevel,
            exercise.VideoUrl,
            exercise.IsActive,
            exercise.CreatedAt,
            exercise.UpdatedAt);

    private static string EscapeLike(string value) => value.Trim()
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("%", "\\%", StringComparison.Ordinal)
        .Replace("_", "\\_", StringComparison.Ordinal);
}
