using Api.Authorization;
using Api.Configuration;
using Api.Contracts.Common;
using Api.Contracts.Training;
using Application.Features.Training.Exercises.ArchiveGlobalExercise;
using Application.Features.Training.Exercises.CreateGlobalExercise;
using Application.Features.Training.Exercises.DeleteGlobalExercise;
using Application.Features.Training.Exercises.GetGlobalExercise;
using Application.Features.Training.Exercises.ListGlobalExercises;
using Application.Features.Training.Exercises.ReactivateGlobalExercise;
using Application.Features.Training.Exercises.UpdateGlobalExercise;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>Expõe a curadoria do catálogo global de exercícios.</summary>
[Route("api/v1/global-exercises")]
[Authorize(ApiPolicyNames.Superuser)]
[AdministrativeContext]
[SensitiveResponse]
public sealed class GlobalExercisesController : ApiControllerBase
{
    /// <summary>Cria um exercício no catálogo global.</summary>
    [HttpPost]
    [ProducesResponseType<GlobalExerciseResponse>(StatusCodes.Status201Created)]
    public Task<IActionResult> CreateAsync(
        [FromBody] CreateGlobalExerciseRequest request,
        [FromServices] CreateGlobalExerciseHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return RespondCreatedAsync(
            handler.HandleAsync(
                new CreateGlobalExerciseCommand(
                    request.Name,
                    request.Description,
                    request.MuscleGroups,
                    request.Equipment,
                    request.DifficultyLevel,
                    request.VideoUrl),
                cancellationToken),
            GlobalExerciseResponse.From,
            exercise => $"/api/v1/global-exercises/{exercise.Id}");
    }

    /// <summary>Lista uma página do catálogo global.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResponse<GlobalExerciseResponse>>(StatusCodes.Status200OK)]
    public Task<IActionResult> ListAsync(
        [FromQuery(Name = "search")] string? search,
        [FromQuery(Name = "activity")] GlobalExerciseActivityFilter activity,
        [FromQuery] PageParameters pageParameters,
        [FromServices] ListGlobalExercisesHandler handler,
        CancellationToken cancellationToken)
    {
        var page = pageParameters.GetEffectivePageNumber();
        var size = pageParameters.GetEffectivePageSize();

        return RespondAsync(
            handler.HandleAsync(
                new ListGlobalExercisesQuery(
                    search,
                    activity,
                    page,
                    size),
                cancellationToken),
            result => PagedResponse<GlobalExerciseResponse>.From(
                result,
                page,
                size,
                GlobalExerciseResponse.From));
    }

    /// <summary>Devolve um exercício global.</summary>
    [HttpGet("{exerciseId:guid}")]
    [ProducesResponseType<GlobalExerciseResponse>(StatusCodes.Status200OK)]
    public Task<IActionResult> GetAsync(
        Guid exerciseId,
        [FromServices] GetGlobalExerciseHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(
                new GetGlobalExerciseQuery(exerciseId),
                cancellationToken),
            GlobalExerciseResponse.From);

    /// <summary>Substitui os campos editáveis de um exercício global.</summary>
    [HttpPatch("{exerciseId:guid}")]
    [ProducesResponseType<GlobalExerciseResponse>(StatusCodes.Status200OK)]
    public Task<IActionResult> UpdateAsync(
        Guid exerciseId,
        [FromBody] UpdateGlobalExerciseRequest request,
        [FromServices] UpdateGlobalExerciseHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return RespondAsync(
            handler.HandleAsync(
                new UpdateGlobalExerciseCommand(
                    exerciseId,
                    request.Name,
                    request.Description,
                    request.MuscleGroups,
                    request.Equipment,
                    request.DifficultyLevel,
                    request.VideoUrl),
                cancellationToken),
            GlobalExerciseResponse.From);
    }

    /// <summary>Arquiva um exercício global, retirando-o dos catálogos ativos.</summary>
    [HttpPost("{exerciseId:guid}/archive")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<IActionResult> ArchiveAsync(
        Guid exerciseId,
        [FromServices] ArchiveGlobalExerciseHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(
                new ArchiveGlobalExerciseCommand(exerciseId),
                cancellationToken));

    /// <summary>Reativa um exercício global arquivado.</summary>
    [HttpPost("{exerciseId:guid}/reactivate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<IActionResult> ReactivateAsync(
        Guid exerciseId,
        [FromServices] ReactivateGlobalExerciseHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(
                new ReactivateGlobalExerciseCommand(exerciseId),
                cancellationToken));

    /// <summary>Elimina definitivamente um exercício global sem referências.</summary>
    [HttpDelete("{exerciseId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<IActionResult> DeleteAsync(
        Guid exerciseId,
        [FromServices] DeleteGlobalExerciseHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(
                new DeleteGlobalExerciseCommand(exerciseId),
                cancellationToken));
}
