using Domain.Entities.Training;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Training;

/// <summary>Locks pessimistas usados pelas escritas de vídeos geridos.</summary>
internal static class ExerciseVideoLocking
{
    /// <summary>Bloqueia o exercício privado do tenant indicado.</summary>
    public static Task<Exercise?> LockPrivateExerciseAsync(
        this PtManagerDbContext dbContext,
        Guid exerciseId,
        Guid trainerId,
        CancellationToken cancellationToken) =>
        dbContext.Exercises
            .FromSqlInterpolated($$"""
                SELECT * FROM exercises
                WHERE id = {{exerciseId}}
                    AND owner_trainer_id = {{trainerId}}
                FOR UPDATE
            """)
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Serializa a quota por personal trainer sem bloquear linhas partilhadas com
    /// outros casos de uso. O lock é libertado no fim da transação.
    /// </summary>
    public static Task AcquireVideoQuotaLockAsync(
        this PtManagerDbContext dbContext,
        Guid trainerId,
        CancellationToken cancellationToken)
    {
        var lockKey = $"exercise-video-quota:{trainerId:N}";
        return dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))",
            cancellationToken);
    }

    /// <summary>Bloqueia um vídeo do owner indicado; owner nulo significa global.</summary>
    public static Task<ExerciseVideo?> LockVideoAsync(
        this PtManagerDbContext dbContext,
        Guid videoId,
        Guid? ownerTrainerId,
        CancellationToken cancellationToken)
    {
        var query = ownerTrainerId.HasValue
            ? dbContext.ExerciseVideos.FromSqlInterpolated($$"""
                SELECT * FROM exercise_videos
                WHERE id = {{videoId}}
                    AND owner_trainer_id = {{ownerTrainerId.Value}}
                FOR UPDATE
            """)
            : dbContext.ExerciseVideos.FromSqlInterpolated($$"""
                SELECT * FROM exercise_videos
                WHERE id = {{videoId}}
                    AND owner_trainer_id IS NULL
                FOR UPDATE
            """);

        return query.IgnoreQueryFilters().SingleOrDefaultAsync(cancellationToken);
    }

    /// <summary>Bloqueia o vídeo Ready de um exercício, se existir.</summary>
    public static Task<ExerciseVideo?> LockReadyVideoAsync(
        this PtManagerDbContext dbContext,
        Guid exerciseId,
        Guid? ownerTrainerId,
        CancellationToken cancellationToken)
    {
        var query = ownerTrainerId.HasValue
            ? dbContext.ExerciseVideos.FromSqlInterpolated($$"""
                SELECT * FROM exercise_videos
                WHERE exercise_id = {{exerciseId}}
                    AND owner_trainer_id = {{ownerTrainerId.Value}}
                    AND status = 'ready'
                FOR UPDATE
            """)
            : dbContext.ExerciseVideos.FromSqlInterpolated($$"""
                SELECT * FROM exercise_videos
                WHERE exercise_id = {{exerciseId}}
                    AND owner_trainer_id IS NULL
                    AND status = 'ready'
                FOR UPDATE
            """);

        return query.IgnoreQueryFilters().SingleOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Confirma e bloqueia o lease do durable job até ao fim da transação. Com o
    /// lock, nem a renovação nem um novo claim podem mudar o owner enquanto a
    /// transição do vídeo é escrita.
    /// </summary>
    public static async Task<bool> LockValidJobLeaseAsync(
        this PtManagerDbContext dbContext,
        Guid jobId,
        Guid leaseOwnerId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var lockedId = await dbContext.Database.SqlQuery<Guid>($"""
            SELECT id AS "Value"
            FROM durable_jobs
            WHERE id = {jobId}
                AND lease_owner_id = {leaseOwnerId}
                AND status = 'processing'
                AND lease_expires_at > {now}
            FOR UPDATE
            """).SingleOrDefaultAsync(cancellationToken);

        return lockedId != Guid.Empty;
    }
}
