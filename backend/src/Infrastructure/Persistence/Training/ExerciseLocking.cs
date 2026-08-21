using Domain.Entities.Training;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Training;

/// <summary>Locks pessimistas do catálogo global de exercícios.</summary>
internal static class ExerciseLocking
{
    public static Task<Exercise?> LockGlobalExerciseAsync(
        this PtManagerDbContext dbContext,
        Guid exerciseId,
        CancellationToken cancellationToken) =>
        dbContext.Exercises.FromSqlInterpolated($$"""
            SELECT * FROM exercises
            WHERE id = {{exerciseId}}
                AND owner_trainer_id IS NULL
            FOR UPDATE
            """)
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(cancellationToken);
}
