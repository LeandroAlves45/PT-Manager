using Api.Authorization;
using Api.Configuration;
using Application.Features.Training.ExerciseVideos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>Vídeos geridos do catálogo global, sem quota, geridos pelo superuser.</summary>
[Route("api/v1/global-exercises/{exerciseId:guid}/video")]
[Authorize(ApiPolicyNames.Superuser)]
[AdministrativeContext]
[SensitiveResponse]
public sealed class GlobalExerciseVideosController : ManagedExerciseVideoControllerBase
{
    protected override ExerciseVideoCatalog Catalog => ExerciseVideoCatalog.Global;

    protected override ExerciseVideoPlaybackAudience PlaybackAudience =>
        ExerciseVideoPlaybackAudience.GlobalCatalog;

    protected override string ExerciseRoutePrefix => "/api/v1/global-exercises";
}
