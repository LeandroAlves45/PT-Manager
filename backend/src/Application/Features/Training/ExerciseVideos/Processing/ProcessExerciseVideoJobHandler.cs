using Application.Features.Jobs.Dispatching;
using Application.Features.Training.ExerciseVideos.Abstractions;
using Domain.ValueObjects;

namespace Application.Features.Training.ExerciseVideos.Processing;

/// <summary>
/// Valida tecnicamente um vídeo em Processing e publica-o ou recusa-o.
/// </summary>
/// <remarks>
/// <para>
/// A leitura do container é remota e acontece sem transação. A publicação ou a
/// recusa só é escrita se o job ainda detiver o lease: a perda do lease nunca
/// publica um vídeo.
/// </para>
/// </remarks>
public sealed class ProcessExerciseVideoJobHandler : IPlatformDurableJobHandler
{
    private readonly IExerciseVideoProcessingStore _store;
    private readonly IVideoMetadataProbe _probe;

    public ProcessExerciseVideoJobHandler(
        IExerciseVideoProcessingStore store,
        IVideoMetadataProbe probe)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _probe = probe ?? throw new ArgumentNullException(nameof(probe));
    }

    public string JobType => ExerciseVideoJobs.ProcessType;
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
        if (video is null || video.Status.IsTerminal)
            return DispatchItemOutcome.Succeeded();

        if (video.Status != ExerciseVideoStatus.Processing ||
            video.StoredSizeBytes is null || video.StoredETag is null)
            return DispatchItemOutcome.PermanentFailure("exercise_video_state_invalid");

        var probe = await _probe.ProbeAsync(
            video.ObjectKey,
            video.OwnerTrainerId,
            video.StoredSizeBytes.Value,
            video.StoredETag,
            cancellationToken);

        var lease = new ExerciseVideoLease(
            video.Id,
            video.OwnerTrainerId,
            job.Id,
            job.LeaseOwnerId,
            job.CorrelationId);

        switch (probe.Status)
        {
            case VideoProbeStatus.Disabled:
            case VideoProbeStatus.TransientFailure:
                return DispatchItemOutcome.TransientFailure(
                    probe.FailureCode ?? "exercise_video_probe_unavailable");

            case VideoProbeStatus.Probed:
                var rejection = ExerciseVideoPolicy.Evaluate(video.ContentType, probe.Metadata!);
                return ExerciseVideoJobOutcomes.FromTransition(rejection is null
                    ? await _store.MarkReadyAsync(lease, probe.Metadata!, cancellationToken)
                    : await _store.TerminateAsync(
                        lease, ExerciseVideoTermination.Rejected, rejection, cancellationToken));

            default:
                return ExerciseVideoJobOutcomes.FromTransition(await _store.TerminateAsync(
                    lease,
                    ExerciseVideoTermination.Rejected,
                    probe.FailureCode ?? "exercise_video_container_unsupported",
                    cancellationToken));
        }
    }
}
