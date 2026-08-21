using Domain.Entities.Nutrition;
using Domain.Entities.Supplements;
using Domain.Entities.Training;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Common;

/// <summary>
/// Locks <c>FOR SHARE</c> batched sobre linhas de catálogo (Food, Supplement,
/// Exercise) referenciadas por um agregado. Os IDs são ordenados antes do
/// lock para que duas transações que referenciem o mesmo conjunto de linhas
/// as bloqueiem sempre pela mesma ordem, evitando deadlock. Um <c>FOR SHARE</c>
/// permite leituras concorrentes mas bloqueia qualquer <c>FOR UPDATE</c>
/// administrativo (Archive/Update/Delete) até a transação do plano terminar.
/// </summary>
internal static class CatalogReferenceLocking
{
    /// <summary>
    /// Bloqueia e devolve os Foods visíveis para o tenant, mantendo-os tracked
    /// para que o interceptor de escrita reutilize a mesma leitura.
    /// </summary>
    public static Task<List<Food>> LockFoodsForShareAsync(
        this PtManagerDbContext dbContext,
        Guid trainerId,
        IReadOnlyCollection<Guid> foodIds,
        CancellationToken cancellationToken)
    {
        var ordered = foodIds.Distinct().OrderBy(id => id).ToArray();
        if (ordered.Length == 0)
            return Task.FromResult(new List<Food>());

        return dbContext.Foods.FromSqlInterpolated($"""
            SELECT * FROM foods
            WHERE id = ANY({ordered})
                AND (owner_trainer_id IS NULL OR owner_trainer_id = {trainerId})
            ORDER BY id
            FOR SHARE
            """).IgnoreQueryFilters().ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Bloqueia e devolve os Supplements visíveis para o tenant, mantendo-os tracked
    /// para evitar uma segunda validação no interceptor.
    /// </summary>
    public static Task<List<Supplement>> LockSupplementsForShareAsync(
        this PtManagerDbContext dbContext,
        Guid trainerId,
        IReadOnlyCollection<Guid> supplementIds,
        CancellationToken cancellationToken)
    {
        var ordered = supplementIds.Distinct().OrderBy(id => id).ToArray();
        if (ordered.Length == 0)
            return Task.FromResult(new List<Supplement>());

        return dbContext.Supplements.FromSqlInterpolated($"""
            SELECT * FROM supplements
            WHERE id = ANY({ordered})
                AND (owner_trainer_id IS NULL OR owner_trainer_id = {trainerId})
            ORDER BY id
            FOR SHARE
            """).IgnoreQueryFilters().ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Bloqueia e devolve os Exercises visíveis para o tenant, mantendo-os tracked
    /// para evitar uma segunda validação no interceptor.
    /// </summary>
    public static Task<List<Exercise>> LockExercisesForShareAsync(
        this PtManagerDbContext dbContext,
        Guid trainerId,
        IReadOnlyCollection<Guid> exerciseIds,
        CancellationToken cancellationToken)
    {
        var ordered = exerciseIds.Distinct().OrderBy(id => id).ToArray();
        if (ordered.Length == 0)
            return Task.FromResult(new List<Exercise>());

        return dbContext.Exercises.FromSqlInterpolated($"""
            SELECT * FROM exercises
            WHERE id = ANY({ordered})
                AND (owner_trainer_id IS NULL OR owner_trainer_id = {trainerId})
            ORDER BY id
            FOR SHARE
            """).IgnoreQueryFilters().ToListAsync(cancellationToken);
    }
}
