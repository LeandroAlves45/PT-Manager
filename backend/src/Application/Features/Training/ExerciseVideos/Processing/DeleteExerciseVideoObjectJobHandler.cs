using Application.Features.Jobs.Dispatching;
using Application.Features.Training.ExerciseVideos.Abstractions;
using Domain.Entities.Training;

namespace Application.Features.Training.ExerciseVideos.Processing;

/// <summary>
/// Elimina, depois do commit, o objeto de um vídeo removido, substituído,
/// recusado ou falhado.
/// </summary>
public sealed class DeleteExerciseVideoObjectJobHandler : IPlatformDurableJobHandler
{
    private readonly IVideoObjectStorage _storage;

    public DeleteExerciseVideoObjectJobHandler(IVideoObjectStorage storage) =>
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));

    public string JobType => ExerciseVideoJobs.DeleteObjectType;
    public int JobVersion => ExerciseVideoJobs.Version;

    public async Task<DispatchItemOutcome> HandleAsync(
        DurableJobEnvelope job,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);

        if (!ExerciseVideoJobOutcomes.IsSupported(job, JobType))
            return DispatchItemOutcome.PermanentFailure("exercise_video_job_contract_mismatch");

        if (!ExerciseVideoJobs.TryReadDeleteObjectPayload(
            job.Payload, out var videoId, out var objectKey))
            return DispatchItemOutcome.PermanentFailure("exercise_video_job_payload_invalid");

        if (!string.Equals(
            objectKey,
            ExerciseVideo.BuildObjectKey(job.TrainerId, videoId),
            StringComparison.Ordinal))
            return DispatchItemOutcome.PermanentFailure("exercise_video_object_key_invalid");

        var deletion = await _storage.DeleteAsync(objectKey, job.TrainerId, cancellationToken);

        return deletion.Status switch
        {
            VideoStorageStatus.Success or VideoStorageStatus.NotFound =>
                DispatchItemOutcome.Succeeded(),

            // Desligado depois de ter havido uploads: repetir é o comportamento certo.
            // Esgotadas as tentativas, o dead letter sinaliza objetos por eliminar.
            VideoStorageStatus.Disabled or VideoStorageStatus.TransientFailure =>
                DispatchItemOutcome.TransientFailure(
                    deletion.FailureCode ?? "exercise_video_deletion_transient"),

            _ => DispatchItemOutcome.PermanentFailure(
                deletion.FailureCode ?? "exercise_video_deletion_failed")
        };
    }
}
