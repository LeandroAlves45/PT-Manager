using Api.Authorization;
using Api.Configuration;
using Api.Contracts.Common;
using Api.Contracts.Training;
using Application.Features.Training.TrainingPlans.ArchiveTrainingPlan;
using Application.Features.Training.TrainingPlans.CreateTrainingPlan;
using Application.Features.Training.TrainingPlans.GetTrainingPlan;
using Application.Features.Training.TrainingPlans.ListTrainingPlans;
using Application.Features.Training.TrainingPlans.ReplaceTrainingPlan;
using Application.Features.Training.TrainingPlans.UpdateTrainingPlanMetadata;
using Application.Features.Training.TrainingPlans.UpdateTrainingPlanStructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>Expõe a prescrição de planos de treino do personal trainer autenticado.</summary>
[Route("api/v1/training-plans")]
[Authorize(ApiPolicyNames.Trainer)]
[SensitiveResponse]
public sealed class TrainingPlansController : ApiControllerBase
{
    /// <summary>Cria um plano de treino e a respetiva estrutura inicial.</summary>
    [HttpPost]
    public Task<IActionResult> CreateAsync(
        [FromBody] CreateTrainingPlanRequest request,
        [FromServices] CreateTrainingPlanHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return RespondCreatedAsync(
            handler.HandleAsync(
                new CreateTrainingPlanCommand(
                    request.ClientId,
                    request.Name,
                    request.Description,
                    request.TrainingModality,
                    request.Notes,
                    request.StartDate,
                    request.EndDate,
                    request.Structure.ToInput()),
                cancellationToken),
            TrainingPlanDetailsResponse.From,
            plan => $"/api/v1/training-plans/{plan.Id}");
    }

    /// <summary>Lista uma página de planos do tenant, opcionalmente por cliente.</summary>
    [HttpGet]
    public Task<IActionResult> ListAsync(
        [FromQuery(Name = "client_id")] Guid? clientId,
        [FromQuery(Name = "search")] string? search,
        [FromQuery(Name = "activity")] TrainingPlanActivityFilter activity,
        [FromQuery] PageParameters pageParameters,
        [FromServices] ListTrainingPlansHandler handler,
        CancellationToken cancellationToken)
    {
        var page = pageParameters.EffectivePageNumber;
        var size = pageParameters.EffectivePageSize;

        return RespondAsync(
            handler.HandleAsync(
                new ListTrainingPlansQuery(
                    clientId,
                    search,
                    activity,
                    page,
                    size),
                cancellationToken),
            result => PagedResponse<TrainingPlanSummaryResponse>.From(
                result,
                page,
                size,
                TrainingPlanSummaryResponse.From));
    }

    /// <summary>Devolve um plano completo com dias, exercícios e séries.</summary>
    [HttpGet("{trainingPlanId:guid}")]
    public Task<IActionResult> GetAsync(
        Guid trainingPlanId,
        [FromServices] GetTrainingPlanHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(
                new GetTrainingPlanQuery(trainingPlanId),
                cancellationToken),
            TrainingPlanDetailsResponse.From);

    /// <summary>Substitui cabeçalho e estrutura do plano numa só operação.</summary>
    [HttpPut("{trainingPlanId:guid}")]
    public Task<IActionResult> ReplaceAsync(
        Guid trainingPlanId,
        [FromBody] ReplaceTrainingPlanRequest request,
        [FromServices] ReplaceTrainingPlanHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return RespondAsync(
            handler.HandleAsync(
                new ReplaceTrainingPlanCommand(
                    trainingPlanId,
                    request.Name,
                    request.Description,
                    request.TrainingModality,
                    request.Notes,
                    request.StartDate,
                    request.EndDate,
                    request.Structure.ToInput()),
                cancellationToken),
            TrainingPlanDetailsResponse.From);
    }

    /// <summary>Atualiza apenas o cabeçalho, sem tocar na estrutura.</summary>
    [HttpPatch("{trainingPlanId:guid}")]
    public Task<IActionResult> UpdateMetadataAsync(
        Guid trainingPlanId,
        [FromBody] UpdateTrainingPlanMetadataRequest request,
        [FromServices] UpdateTrainingPlanMetadataHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return RespondAsync(
            handler.HandleAsync(
                new UpdateTrainingPlanMetadataCommand(
                    trainingPlanId,
                    request.Name,
                    request.Description,
                    request.TrainingModality,
                    request.Notes,
                    request.StartDate,
                    request.EndDate),
                cancellationToken),
            TrainingPlanDetailsResponse.From);
    }

    /// <summary>Substitui apenas a estrutura, sem tocar no cabeçalho.</summary>
    [HttpPut("{trainingPlanId:guid}/structure")]
    public Task<IActionResult> UpdateStructureAsync(
        Guid trainingPlanId,
        [FromBody] UpdateTrainingPlanStructureRequest request,
        [FromServices] UpdateTrainingPlanStructureHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return RespondAsync(
            handler.HandleAsync(
                new UpdateTrainingPlanStructureCommand(
                    trainingPlanId,
                    request.Structure.ToInput()),
                cancellationToken),
            TrainingPlanDetailsResponse.From);
    }

    /// <summary>Arquiva um plano de treino sem o eliminar.</summary>
    [HttpPost("{trainingPlanId:guid}/archive")]
    public Task<IActionResult> ArchiveAsync(
        Guid trainingPlanId,
        [FromServices] ArchiveTrainingPlanHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(
                new ArchiveTrainingPlanCommand(trainingPlanId),
                cancellationToken));
}
