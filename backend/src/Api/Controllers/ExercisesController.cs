using Api.Authorization;
using Api.Configuration;
using Api.Contracts.Common;
using Api.Contracts.Training;
using Application.Features.Training.Exercises.ArchiveExercise;
using Application.Features.Training.Exercises.CreateExercise;
using Application.Features.Training.Exercises.GetExercise;
using Application.Features.Training.Exercises.ListExercises;
using Application.Features.Training.Exercises.ReactivateExercise;
using Application.Features.Training.Exercises.UpdateExercise;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>Expõe o catálogo de exercícios visível ao personal trainer autenticado.</summary>
[Route("api/v1/exercises")]
[Authorize(ApiPolicyNames.Trainer)]
[SensitiveResponse]
public sealed class ExercisesController : ApiControllerBase
{
    /// <summary>Cria um exercício privado no tenant efetivo.</summary>
    [HttpPost]
    public Task<IActionResult> CreateAsync(
        [FromBody] CreateExerciseRequest request,
        [FromServices] CreateExerciseHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return RespondCreatedAsync(
            handler.HandleAsync(
                new CreateExerciseCommand(
                    request.Name,
                    request.Description,
                    request.MuscleGroups,
                    request.Equipment,
                    request.DifficultyLevel,
                    request.VideoUrl),
                cancellationToken),
            ExerciseResponse.From,
            exercise => $"/api/v1/exercises/{exercise.Id}");
    }

    /// <summary>Lista uma página de exercícios globais ativos e privados do tenant.</summary>
    [HttpGet]
    public Task<IActionResult> ListAsync(
        [FromQuery(Name = "search")] string? search,
        [FromQuery(Name = "activity")] ExerciseActivityFilter activity,
        [FromQuery] PageParameters pageParameters,
        [FromServices] ListExercisesHandler handler,
        CancellationToken cancellationToken)
    {
        var page = pageParameters.EffectivePageNumber;
        var size = pageParameters.EffectivePageSize;

        return RespondAsync(
            handler.HandleAsync(
                new ListExercisesQuery(
                    search,
                    activity,
                    page,
                    size),
                cancellationToken),
            result => PagedResponse<ExerciseResponse>.From(
                result,
                page,
                size,
                ExerciseResponse.From));
    }

    /// <summary>Devolve um exercício visível ao tenant efetivo.</summary>
    [HttpGet("{exerciseId:guid}")]
    public Task<IActionResult> GetAsync(
        Guid exerciseId,
        [FromServices] GetExerciseHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(
                new GetExerciseQuery(exerciseId),
                cancellationToken),
            ExerciseResponse.From);

    /// <summary>Substitui os campos editáveis de um exercício privado.</summary>
    [HttpPatch("{exerciseId:guid}")]
    public Task<IActionResult> UpdateAsync(
        Guid exerciseId,
        [FromBody] UpdateExerciseRequest request,
        [FromServices] UpdateExerciseHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return RespondAsync(
            handler.HandleAsync(
                new UpdateExerciseCommand(
                    exerciseId,
                    request.Name,
                    request.Description,
                    request.MuscleGroups,
                    request.Equipment,
                    request.DifficultyLevel,
                    request.VideoUrl),
                cancellationToken),
            ExerciseResponse.From);
    }

    /// <summary>Arquiva um exercício privado sem o eliminar.</summary>
    [HttpPost("{exerciseId:guid}/archive")]
    public Task<IActionResult> ArchiveAsync(
        Guid exerciseId,
        [FromServices] ArchiveExerciseHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(
                new ArchiveExerciseCommand(exerciseId),
                cancellationToken));

    /// <summary>Reativa um exercício privado arquivado.</summary>
    [HttpPost("{exerciseId:guid}/reactivate")]
    public Task<IActionResult> ReactivateAsync(
        Guid exerciseId,
        [FromServices] ReactivateExerciseHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(
                new ReactivateExerciseCommand(exerciseId),
                cancellationToken));
}
