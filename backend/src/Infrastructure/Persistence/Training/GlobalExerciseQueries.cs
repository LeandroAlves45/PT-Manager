using System.Linq.Expressions;
using Application.Features.Training.Exercises.Abstractions;
using Application.Features.Training.Exercises.Dtos;
using Application.Features.Training.Exercises.ListGlobalExercises;
using Application.Pagination;
using Domain.Entities.Training;
using Infrastructure.Data;
using Infrastructure.Persistence.Common;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Training;

/// <summary>Consulta exclusivamente exercícios globais para administração.</summary>
internal sealed class GlobalExerciseQueries : IGlobalExerciseQueries
{
    private readonly PtManagerDbContext _dbContext;

    public GlobalExerciseQueries(PtManagerDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public Task<GlobalExerciseDto?> GetAsync(
        Guid exerciseId,
        CancellationToken cancellationToken) =>
        BaseQuery()
            .Where(exercise => exercise.Id == exerciseId)
            .Select(Projection)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<PageResult<GlobalExerciseDto>> ListAsync(
        string? search,
        GlobalExerciseActivityFilter activity,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        var query = activity switch
        {
            GlobalExerciseActivityFilter.Active => BaseQuery().Where(exercise => exercise.IsActive),
            GlobalExerciseActivityFilter.Archived => BaseQuery().Where(exercise => !exercise.IsActive),
            GlobalExerciseActivityFilter.All => BaseQuery(),
            _ => throw new ArgumentOutOfRangeException(nameof(activity))
        };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = LikeSearchPattern.Build(search);
            query = query.Where(exercise =>
                EF.Functions.ILike(exercise.Name, pattern, LikeSearchPattern.LikeEscapeCharacter)
                || exercise.Description != null &&
                EF.Functions.ILike(exercise.Description, pattern, LikeSearchPattern.LikeEscapeCharacter));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(exercise => exercise.Name)
            .ThenBy(exercise => exercise.Id)
            .Skip((page.PageNumber - 1) * page.PageSize)
            .Take(page.PageSize)
            .Select(Projection)
            .ToListAsync(cancellationToken);

        return new PageResult<GlobalExerciseDto>(items, totalCount);
    }

    private IQueryable<Exercise> BaseQuery() =>
        _dbContext.Exercises
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(exercise => exercise.OwnerTrainerId == null);

    private static Expression<Func<Exercise, GlobalExerciseDto>> Projection =>
        exercise => new GlobalExerciseDto(
            exercise.Id,
            exercise.Name,
            exercise.Description,
            exercise.MuscleGroups,
            exercise.Equipment,
            exercise.DifficultyLevel,
            exercise.VideoUrl,
            exercise.IsActive,
            exercise.CreatedAt,
            exercise.UpdatedAt);
}
