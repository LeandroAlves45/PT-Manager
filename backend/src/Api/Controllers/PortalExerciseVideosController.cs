using Api.Authorization;
using Api.Configuration;
using Api.Contracts.Training;
using Application.Features.Training.ExerciseVideos;
using Application.Features.Training.ExerciseVideos.GetExerciseVideoPlayback;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>Reprodução, pelo cliente, dos vídeos dos exercícios do seu plano ativo.</summary>
[Route("api/v1/portal/my-plan/exercises/{exerciseId:guid}/video")]
[Authorize(ApiPolicyNames.Client)]
[SensitiveResponse]
public sealed class PortalExerciseVideosController : ApiControllerBase
{
    /// <summary>
    /// Emite uma URL assinada de curta duração. Um exercício fora do plano ativo
    /// é indistinguível de um exercício sem vídeo.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<ExerciseVideoPlaybackResponse>(StatusCodes.Status200OK)]
    public Task<IActionResult> GetPlaybackAsync(
        Guid exerciseId,
        [FromServices] GetExerciseVideoPlaybackHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(
                new GetExerciseVideoPlaybackQuery(
                    ExerciseVideoPlaybackAudience.Client, exerciseId),
                cancellationToken),
            ExerciseVideoPlaybackResponse.From);
}
