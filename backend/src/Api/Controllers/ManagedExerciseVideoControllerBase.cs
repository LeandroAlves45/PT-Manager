using Api.Configuration;
using Api.Contracts.Training;
using Application.Features.Training.ExerciseVideos;
using Application.Features.Training.ExerciseVideos.CompleteExerciseVideoUpload;
using Application.Features.Training.ExerciseVideos.GetExerciseVideoPlayback;
using Application.Features.Training.ExerciseVideos.GetExerciseVideoUpload;
using Application.Features.Training.ExerciseVideos.RemoveExerciseVideo;
using Application.Features.Training.ExerciseVideos.RequestExerciseVideoUpload;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Api.Controllers;

/// <summary>
/// Operações de gestão de vídeo partilhadas pelo catálogo privado e pelo global.
/// </summary>
/// <remarks>
/// Cada controller concreto fixa rota, política de autorização e catálogo. O
/// catálogo nunca vem do pedido: um personal trainer não consegue escrever no global
/// alterando o body ou a query.
/// </remarks>
public abstract class ManagedExerciseVideoControllerBase : ApiControllerBase
{
    /// <summary>Catálogo autorizado pelo controller concreto.</summary>
    protected abstract ExerciseVideoCatalog Catalog { get; }

    /// <summary>Audiencia de reprodução correspondente ao catálogo.</summary>
    protected abstract ExerciseVideoPlaybackAudience PlaybackAudience { get; }

    /// <summary>Prefixo da rota do exercício, usado no header location.</summary>
    protected abstract string ExerciseRoutePrefix { get; }

    /// <summary>
    /// Regista um vídeo pendente e devolve a autorização de upload direto para
    /// o storage privado.
    /// </summary>
    [HttpPost("uploads")]
    [ProducesResponseType<ExerciseVideoUploadResponse>(StatusCodes.Status201Created)]
    [EnableRateLimiting(ApiRateLimitPolicyNames.VideoUpload)]
    public Task<IActionResult> RequestUploadAsync(
        Guid exerciseId,
        [FromBody] CreateExerciseVideoUploadRequest request,
        [FromServices] RequestExerciseVideoUploadHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return RespondCreatedAsync(
            handler.HandleAsync(
                new RequestExerciseVideoUploadCommand(
                    Catalog,
                    exerciseId,
                    request.ContentType,
                    request.SizeBytes),
                cancellationToken),
            ExerciseVideoUploadResponse.From,
            upload => $"{ExerciseRoutePrefix}/{exerciseId}/video/uploads/{upload.Video.Id}");
    }

    /// <summary>Devolve o estado técnico de um upload.</summary>
    [HttpGet("uploads/{videoId:guid}")]
    [ProducesResponseType<ExerciseVideoResponse>(StatusCodes.Status200OK)]
    public Task<IActionResult> GetUploadAsync(
        Guid exerciseId,
        Guid videoId,
        [FromServices] GetExerciseVideoUploadHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(
                new GetExerciseVideoUploadQuery(Catalog, exerciseId, videoId),
                cancellationToken),
            ExerciseVideoResponse.From);

    /// <summary>
    /// Confirma o upload no fornecedor e agenda a validação técnica. Repetir a
    /// chamada devolve o estado atual.
    /// </summary>
    [HttpPost("uploads/{videoId:guid}/complete")]
    [ProducesResponseType<ExerciseVideoResponse>(StatusCodes.Status200OK)]
    [EnableRateLimiting(ApiRateLimitPolicyNames.VideoUpload)]
    public Task<IActionResult> CompleteUploadAsync(
        Guid exerciseId,
        Guid videoId,
        [FromServices] CompleteExerciseVideoUploadHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(
                new CompleteExerciseVideoUploadCommand(
                    Catalog,
                    exerciseId,
                    videoId),
                cancellationToken),
            ExerciseVideoResponse.From);

    /// <summary>Emite uma URL assinada de curta duração para o vídeo Ready.</summary>
    [HttpGet]
    [ProducesResponseType<ExerciseVideoPlaybackResponse>(StatusCodes.Status200OK)]
    public Task<IActionResult> GetPlaybackAsync(
        Guid exerciseId,
        [FromServices] GetExerciseVideoPlaybackHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(
                new GetExerciseVideoPlaybackQuery(PlaybackAudience, exerciseId),
                cancellationToken),
            ExerciseVideoPlaybackResponse.From);

    /// <summary>Remove o vídeo Ready; o objeto é eliminado depois do commit.</summary>
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<IActionResult> RemoveAsync(
        Guid exerciseId,
        [FromServices] RemoveExerciseVideoHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(
                new RemoveExerciseVideoCommand(Catalog, exerciseId),
                cancellationToken));
}
