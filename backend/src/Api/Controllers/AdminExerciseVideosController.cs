using Api.Authorization;
using Api.Configuration;
using Api.Contracts.Training;
using Application.Features.Training.ExerciseVideos;
using Application.Features.Training.ExerciseVideos.GetExerciseVideoPlayback;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Api.Controllers;

/// <summary>
/// Reprodução administrativa de qualquer vídeo gerido, incluindo vídeos privados
/// de personal trainers.
/// </summary>
/// <remarks>
/// Leitura sem auditoria persistida, por decisão aprovada; a query regista um
/// evento estruturado com o ator e o vídeo.
/// </remarks>
[Route("api/v1/admin/content-moderation/exercises/{exerciseId:guid}/video")]
[Authorize(ApiPolicyNames.AdministrativeContext)]
[AdministrativeContext]
[EnableRateLimiting(ApiRateLimitPolicyNames.Moderation)]
[SensitiveResponse]
public sealed class AdminExerciseVideosController : ApiControllerBase
{
    /// <summary>Emite uma URL assinada de curta duração para o vídeo Ready.</summary>
    [HttpGet]
    [ProducesResponseType<ExerciseVideoPlaybackResponse>(StatusCodes.Status200OK)]
    public Task<IActionResult> GetPlaybackAsync(
        Guid exerciseId,
        [FromServices] GetExerciseVideoPlaybackHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(
                new GetExerciseVideoPlaybackQuery(
                    ExerciseVideoPlaybackAudience.Administrative, exerciseId),
                cancellationToken),
            ExerciseVideoPlaybackResponse.From);
}
