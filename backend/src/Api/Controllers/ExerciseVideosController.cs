using Api.Authorization;
using Api.Configuration;
using Application.Features.Training.ExerciseVideos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>Vídeos geridos dos exercícios privados do personal trainer autenticado.</summary>
[Route("api/v1/exercises/{exerciseId:guid}/video")]
[Authorize(ApiPolicyNames.Trainer)]
[SensitiveResponse]
public sealed class ExerciseVideosController : ManagedExerciseVideoControllerBase
{
    protected override ExerciseVideoCatalog Catalog => ExerciseVideoCatalog.Private;

    protected override ExerciseVideoPlaybackAudience PlaybackAudience =>
        ExerciseVideoPlaybackAudience.Trainer;

    protected override string ExerciseRoutePrefix => "/api/v1/exercises";
}
