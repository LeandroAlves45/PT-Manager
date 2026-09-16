using Application.Common.Abstractions;
using Application.Features.Jobs.Dispatching;
using Application.Features.Training.ExerciseVideos.Abstractions;
using Domain.ValueObjects;

namespace Application.Features.Training.ExerciseVideos.Processing;

/// <summary>
/// Limpa, de forma idempotente, um vídeo que não terminou dentro da janela.
/// </summary>
public sealed class ExpireExerciseVideoUploadJobHandler : IPlatformDurableJobHandler
{
    private readonly IExerciseVideoProcessingStore _store;
    private readonly IClock _clock;

    public ExpireExerciseVideoUploadJobHandler(
        IExerciseVideoProcessingStore store,
        IClock clock)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public string JobType => ExerciseVideoJobs.ExpireType;
    public int JobVersion => ExerciseVideoJobs.Version;

    public async Task<DispatchItemOutcome> HandleAsync(
        DurableJobEnvelope job,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);

        if (!ExerciseVideoJobOutcomes.IsSupported(job, JobType))
            return DispatchItemOutcome.PermanentFailure("exercise_video_job_contract_mismatch");

        if (!ExerciseVideoJobs.TryReadVideoPayload(job.Payload, out var videoId))
            return DispatchItemOutcome.PermanentFailure("exercise_video_job_payload_invalid");

        var video = await _store.FindAsync(videoId, job.TrainerId, cancellationToken);
        if (video is null || !video.Status.IsInFlight)
            return DispatchItemOutcome.Succeeded();

        // Defesa contra um agendamento incorreto: nunca termina um upload cuja
        // janela ainda está aberta; repete mais tarde.
        if (video.IsUploadWindowOpen(_clock.UtcNow))
            return DispatchItemOutcome.TransientFailure("exercise_video_upload_window_open");

        var failureCode = video.Status == ExerciseVideoStatus.Pending
            ? "exercise_video_upload_abandoned"
            : "exercise_video_processing_timeout";

        var transition = await _store.TerminateAsync(
            new ExerciseVideoLease(
                video.Id,
                video.OwnerTrainerId,
                job.Id,
                job.LeaseOwnerId,
                job.CorrelationId),
            ExerciseVideoTermination.Failed,
            failureCode,
            cancellationToken);

        return ExerciseVideoJobOutcomes.FromTransition(transition);
    }
}
