using Api.Authorization;
using Api.Configuration;
using Api.Contracts.Common;
using Api.Contracts.Training;
using Application.Features.Training.ExerciseSetLogs.CorrectExerciseSetLog;
using Application.Features.Training.ExerciseSetLogs.ListExerciseSetLogs;
using Application.Features.Training.ExerciseSetLogs.RecordExerciseSetLog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>Expõe o registo de séries realizadas, feito pelo personal trainer.</summary>
[Route("api/v1/exercise-set-logs")]
[Authorize(ApiPolicyNames.Trainer)]
[SensitiveResponse]
public sealed class ExerciseSetLogsController : ApiControllerBase
{
    /// <summary>Regista uma série efetivamente realizada contra uma prescrição existente.</summary>
    [HttpPost]
    public Task<IActionResult> RecordAsync(
        [FromBody] RegisterExerciseSetLogRequest request,
        [FromServices] RecordExerciseSetLogHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return RespondCreatedAsync(
            handler.HandleAsync(
                new RecordExerciseSetLogCommand(
                    request.TrainingPlanDayExerciseId,
                    request.SetNumber,
                    request.WeightKg,
                    request.RepsDone,
                    request.Notes,
                    request.PerformedAt),
                cancellationToken),
            ExerciseSetLogResponse.From,
            log => $"/api/v1/exercise-set-logs/{log.Id}");
    }

    /// <summary>Corrige os valores de um registo existente.</summary>
    [HttpPatch("{exerciseSetLogId:guid}")]
    public Task<IActionResult> CorrectAsync(
        Guid exerciseSetLogId,
        [FromBody] CorrectExerciseSetLogRequest request,
        [FromServices] CorrectExerciseSetLogHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return RespondAsync(
            handler.HandleAsync(
                new CorrectExerciseSetLogCommand(
                    exerciseSetLogId,
                    request.WeightKg,
                    request.RepsDone,
                    request.Notes,
                    request.PerformedAt),
                cancellationToken),
            ExerciseSetLogResponse.From);
    }

    /// <summary>Lista os registos de um cliente, com filtro temporal opcional.</summary>
    [HttpGet]
    public Task<IActionResult> ListAsync(
        [FromQuery(Name = "client_id")] Guid clientId,
        [FromQuery(Name = "training_plan_id")] Guid? trainingPlanId,
        [FromQuery(Name = "performed_from")] DateTimeOffset? performedFrom,
        [FromQuery(Name = "performed_to")] DateTimeOffset? performedTo,
        [FromQuery] PageParameters pageParameters,
        [FromServices] ListExerciseSetLogsHandler handler,
        CancellationToken cancellationToken)
    {
        var page = pageParameters.EffectivePageNumber;
        var size = pageParameters.EffectivePageSize;

        return RespondAsync(
            handler.HandleAsync(
                new ListExerciseSetLogsQuery(
                    clientId,
                    trainingPlanId,
                    performedFrom,
                    performedTo,
                    page,
                    size),
                cancellationToken),
            result => PagedResponse<ExerciseSetLogResponse>.From(
                result,
                page,
                size,
                ExerciseSetLogResponse.From));
    }
}
