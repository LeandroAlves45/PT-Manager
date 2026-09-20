using Application.Features.Administration.ContentModeration.Abstractions;
using Application.Features.Administration.ContentModeration.Dtos;
using Application.Features.Administration.ContentModeration.ListModerationQueue;
using Application.Pagination;
using Domain.ValueObjects;
using Infrastructure.Data;
using Infrastructure.Persistence.Common;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Administration;

/// <summary>
/// Fila de moderação do superuser. É uma leitura deliberadamente transversal a tenants, por
/// isso usa <c>IgnoreQueryFilters</c>; em troca filtra sempre <c>OwnerTrainerId != null</c>
/// (só conteúdo privado) e junta o nome do personal trainer dono. A autorização é feita no handler.
/// </summary>
internal sealed class ModerationQueueQueries : IModerationQueueQueries
{
    private readonly PtManagerDbContext _dbContext;

    public ModerationQueueQueries(PtManagerDbContext dbContext) =>
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    public Task<PageResult<ModerationQueueItemDto>> ListAsync(
        ModerationContentKind kind,
        ModerationStatusFilter status,
        string? search,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);

        return kind switch
        {
            ModerationContentKind.Food => ListFoodAsync(status, search, page, cancellationToken),
            ModerationContentKind.Exercise => ListExerciseAsync(status, search, page, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }

    private async Task<PageResult<ModerationQueueItemDto>> ListFoodAsync(
        ModerationStatusFilter status,
        string? search,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.Foods
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(food => food.OwnerTrainerId != null);

        query = status switch
        {
            ModerationStatusFilter.Blocked => query.Where(food =>
                food.PlatformEnforcementStatus == PlatformEnforcementStatus.Blocked),
            ModerationStatusFilter.Allowed => query.Where(food =>
                food.PlatformEnforcementStatus == PlatformEnforcementStatus.Allowed),
            ModerationStatusFilter.All => query,
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = LikeSearchPattern.Build(search);
            query = query.Where(food => EF.Functions.ILike(
                food.Name, pattern, LikeSearchPattern.LikeEscapeCharacter));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            // Conteúdo mexido há menos tempo primeiro: é o que o admin quer rever.
            .OrderByDescending(food => food.UpdatedAt)
            .ThenBy(food => food.Id)
            .Skip((page.PageNumber - 1) * page.PageSize)
            .Take(page.PageSize)
            .Select(food => new ModerationQueueItemDto(
                food.Id,
                food.Name,
                food.OwnerTrainerId!.Value,
                _dbContext.Users
                    .Where(user => user.Id == food.OwnerTrainerId!.Value)
                    .Select(user => user.FullName)
                    .FirstOrDefault(),
                food.IsActive,
                food.PlatformEnforcementStatus.Value,
                food.PlatformEnforcementReason != null
                    ? food.PlatformEnforcementReason.Value
                    : null,
                food.PlatformEnforcedAt,
                food.CreatedAt,
                food.UpdatedAt))
            .ToListAsync(cancellationToken);

        return new PageResult<ModerationQueueItemDto>(items, totalCount);
    }

    private async Task<PageResult<ModerationQueueItemDto>> ListExerciseAsync(
        ModerationStatusFilter status,
        string? search,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.Exercises
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(exercise => exercise.OwnerTrainerId != null);

        query = status switch
        {
            ModerationStatusFilter.Blocked => query.Where(exercise =>
                exercise.PlatformEnforcementStatus == PlatformEnforcementStatus.Blocked),
            ModerationStatusFilter.Allowed => query.Where(exercise =>
                exercise.PlatformEnforcementStatus == PlatformEnforcementStatus.Allowed),
            ModerationStatusFilter.All => query,
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = LikeSearchPattern.Build(search);
            query = query.Where(exercise => EF.Functions.ILike(
                exercise.Name, pattern, LikeSearchPattern.LikeEscapeCharacter));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(exercise => exercise.UpdatedAt)
            .ThenBy(exercise => exercise.Id)
            .Skip((page.PageNumber - 1) * page.PageSize)
            .Take(page.PageSize)
            .Select(exercise => new ModerationQueueItemDto(
                exercise.Id,
                exercise.Name,
                exercise.OwnerTrainerId!.Value,
                _dbContext.Users
                    .Where(user => user.Id == exercise.OwnerTrainerId!.Value)
                    .Select(user => user.FullName)
                    .FirstOrDefault(),
                exercise.IsActive,
                exercise.PlatformEnforcementStatus.Value,
                exercise.PlatformEnforcementReason != null
                    ? exercise.PlatformEnforcementReason.Value
                    : null,
                exercise.PlatformEnforcedAt,
                exercise.CreatedAt,
                exercise.UpdatedAt))
            .ToListAsync(cancellationToken);

        return new PageResult<ModerationQueueItemDto>(items, totalCount);
    }
}
