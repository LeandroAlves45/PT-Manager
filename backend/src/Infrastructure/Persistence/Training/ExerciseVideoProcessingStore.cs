using System.Data;
using Application.Common.Abstractions;
using Application.Features.Training.ExerciseVideos;
using Application.Features.Training.ExerciseVideos.Abstractions;
using Domain.Entities.Jobs;
using Domain.Entities.Training;
using Domain.ValueObjects;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Infrastructure.Persistence.Training;

/// <summary>
/// Transições dos durable jobs de vídeo, condicionadas pelo lease na mesma
/// transação.
/// </summary>
/// <remarks>
/// A ordem dos locks é fixa: primeiro o job, depois o vídeo, depois o vídeo Ready
/// anterior. Com o job bloqueado, nem a renovação nem um novo claim mudam o owner
/// do lease enquanto a transição é escrita.
/// </remarks>
internal sealed class ExerciseVideoProcessingStore : IExerciseVideoProcessingStore
{
    private readonly PtManagerDbContext _dbContext;
    private readonly IClock _clock;

    public ExerciseVideoProcessingStore(
        PtManagerDbContext dbContext,
        IClock clock)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public Task<ExerciseVideo?> FindAsync(
        Guid videoId,
        Guid? ownerTrainerId,
        CancellationToken cancellationToken) =>
        // O owner vem do job persistido. Um vídeo de outro tenant é igual a um
        // vídeo inexistente para este job.
        _dbContext.ExerciseVideos
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(video => video.Id == videoId && video.OwnerTrainerId == ownerTrainerId)
            .SingleOrDefaultAsync(cancellationToken);

    public Task<ExerciseVideoJobTransitionStatus> MarkReadyAsync(
        ExerciseVideoLease lease,
        VideoTechnicalMetadata metadata,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentNullException.ThrowIfNull(metadata);

        return ExecuteAsync(
            async token =>
            {
                var now = _clock.UtcNow;
                if (!await _dbContext.LockValidJobLeaseAsync(
                    lease.JobId, lease.LeaseOwnerId, now, token))
                    return ExerciseVideoJobTransitionStatus.LeaseLost;

                var video = await _dbContext.LockVideoAsync(lease.VideoId, lease.OwnerTrainerId, token);
                if (video is null)
                    return ExerciseVideoJobTransitionStatus.NotFound;

                if (video.Status.IsTerminal)
                    return ExerciseVideoJobTransitionStatus.AlreadyTerminal;
                if (video.Status != ExerciseVideoStatus.Processing)
                    return ExerciseVideoJobTransitionStatus.InvalidState;

                var previous = await _dbContext.LockReadyVideoAsync(
                    video.ExerciseId, video.OwnerTrainerId, token);
                if (previous is not null)
                {
                    // Remove() entra no interceptor; ExecuteDelete não. Na mesma
                    // transação o índice parcial de Ready só vê o vídeo novo.
                    _dbContext.ExerciseVideos.Remove(previous);
                    _dbContext.DurableJobs.Add(CreateObjectDeletion(previous, lease.CorrelationId, now));
                }

                video.MarkReady(
                    metadata.DurationMilliseconds,
                    metadata.Width,
                    metadata.Height,
                    metadata.VideoCodec,
                    metadata.AudioCodec,
                    now);

                await _dbContext.SaveChangesAsync(token);
                return ExerciseVideoJobTransitionStatus.Applied;
            },
            token => _dbContext.ExerciseVideos
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync(
                    video => video.Id == lease.VideoId && video.Status == ExerciseVideoStatus.Ready,
                    token),
            cancellationToken);
    }

    public Task<ExerciseVideoJobTransitionStatus> TerminateAsync(
        ExerciseVideoLease lease,
        ExerciseVideoTermination termination,
        string failureCode,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lease);

        var deleteKey = ExerciseVideoJobs.DeleteObjectIdempotencyKey(lease.VideoId);
        return ExecuteAsync(
            async token =>
            {
                var now = _clock.UtcNow;
                if (!await _dbContext.LockValidJobLeaseAsync(
                    lease.JobId, lease.LeaseOwnerId, now, token))
                    return ExerciseVideoJobTransitionStatus.LeaseLost;

                var video = await _dbContext.LockVideoAsync(lease.VideoId, lease.OwnerTrainerId, token);
                if (video is null)
                    return ExerciseVideoJobTransitionStatus.NotFound;
                if (video.Status.IsTerminal)
                    return ExerciseVideoJobTransitionStatus.AlreadyTerminal;

                if (termination == ExerciseVideoTermination.Rejected)
                    video.Reject(failureCode, now);
                else
                    video.Fail(failureCode, now);

                _dbContext.DurableJobs.Add(CreateObjectDeletion(video, lease.CorrelationId, now));

                await _dbContext.SaveChangesAsync(token);
                return ExerciseVideoJobTransitionStatus.Applied;
            },
            token => _dbContext.DurableJobs
                .AsNoTracking()
                .AnyAsync(job => job.IdempotencyKey == deleteKey, token),
            cancellationToken);
    }

    private static DurableJob CreateObjectDeletion(
        ExerciseVideo video, Guid correlationId, DateTime now) =>
        new(
            video.OwnerTrainerId,
            ExerciseVideoJobs.DeleteObjectType,
            ExerciseVideoJobs.Version,
            ExerciseVideoJobs.SerializeDeleteObjectPayload(video.Id, video.ObjectKey),
            ExerciseVideoJobs.DeleteObjectIdempotencyKey(video.Id),
            correlationId,
            now,
            now);

    private Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        Func<CancellationToken, Task<bool>> verifySucceeded,
        CancellationToken cancellationToken)
    {
        Func<CancellationToken, Task<T>> attempt = async operationToken =>
        {
            _dbContext.ChangeTracker.Clear();
            return await operation(operationToken);
        };

        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return strategy.ExecuteInTransactionAsync(
            attempt,
            verifySucceeded,
            IsolationLevel.ReadCommitted,
            cancellationToken);
    }
}
