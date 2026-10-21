using Api.Authorization;
using Api.Configuration;
using Api.Contracts.Common;
using Api.Contracts.Training;
using Application.Features.Training.WorkoutCompletions.ListWorkoutCompletions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>
/// Leitura, pelo personal trainer, das conclusões de treino dos seus clientes e das notas escritas ao
/// concluir. A escrita é só do cliente, no portal.
/// </summary>
[Route("api/v1/workout-completions")]
[Authorize(ApiPolicyNames.Trainer)]
[SensitiveResponse]
public sealed class WorkoutCompletionsController : ApiControllerBase
{
    /// <summary>Lista as conclusões de um cliente, opcionalmente de um plano.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResponse<WorkoutCompletionResponse>>(StatusCodes.Status200OK)]
    public Task<IActionResult> ListAsync(
        [FromQuery(Name = "client_id")] Guid clientId,
        [FromQuery(Name = "training_plan_id")] Guid? trainingPlanId,
        [FromQuery] PageParameters pageParameters,
        [FromServices] ListWorkoutCompletionsHandler handler,
        CancellationToken cancellationToken)
    {
        var page = pageParameters.GetEffectivePageNumber();
        var size = pageParameters.GetEffectivePageSize();

        return RespondAsync(
            handler.HandleAsync(
                new ListWorkoutCompletionsQuery(clientId, trainingPlanId, page, size),
                cancellationToken),
            result => PagedResponse<WorkoutCompletionResponse>.From(
                result,
                page,
                size,
                WorkoutCompletionResponse.From));
    }
}
