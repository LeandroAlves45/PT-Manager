using Domain.Entities.Nutrition;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Nutrition;

/// <summary>Locks pessimistas do catálogo global de alimentos.</summary>
internal static class FoodLocking
{
    public static Task<Food?> LockGlobalFoodAsync(
        this PtManagerDbContext dbContext,
        Guid foodId,
        CancellationToken cancellationToken) =>
        dbContext.Foods.FromSqlInterpolated($$"""
            SELECT * FROM foods
            WHERE id = {{foodId}}
                AND owner_trainer_id IS NULL
            FOR UPDATE
            """)
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(cancellationToken);
}
