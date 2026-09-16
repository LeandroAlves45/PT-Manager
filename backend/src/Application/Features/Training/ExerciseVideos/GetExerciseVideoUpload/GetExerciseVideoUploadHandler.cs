using Application.Common.Abstractions;
using Application.Features.Training.ExerciseVideos.Abstractions;
using Application.Features.Training.ExerciseVideos.Dtos;
using Application.Results;

namespace Application.Features.Training.ExerciseVideos.GetExerciseVideoUpload;

/// <summary>
/// Devolve o estado de um upload ao seu gestor. Um upload de outro tenant ou de
/// outro catálogo é indistinguível de um upload inexistente.
/// </summary>
public sealed class GetExerciseVideoUploadHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IExerciseVideoStore _store;

    public GetExerciseVideoUploadHandler(ITenantContext tenantContext, IExerciseVideoStore store)
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<Result<ExerciseVideoDto>> HandleAsync(
        GetExerciseVideoUploadQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.ExerciseId == Guid.Empty)
            return Result<ExerciseVideoDto>.Failure(TrainingErrors.ExerciseIdRequired());
        if (query.VideoId == Guid.Empty)
            return Result<ExerciseVideoDto>.Failure(ExerciseVideoErrors.VideoIdRequired());

        var actor = ExerciseVideoActors.ResolveWriter(_tenantContext, query.Catalog);
        if (!actor.IsSuccess)
            return Result<ExerciseVideoDto>.Failure(actor.Error!);

        var video = await _store.FindUploadAsync(
            query.Catalog,
            query.ExerciseId,
            query.VideoId,
            actor.Value.OwnerTrainerId,
            cancellationToken);

        return video is null
            ? Result<ExerciseVideoDto>.Failure(ExerciseVideoErrors.UploadNotFound)
            : Result<ExerciseVideoDto>.Success(video.ToDto());
    }
}
