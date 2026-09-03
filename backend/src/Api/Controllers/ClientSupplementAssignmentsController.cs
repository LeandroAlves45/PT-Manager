using Api.Authorization;
using Api.Configuration;
using Api.Contracts.Common;
using Api.Contracts.Supplements;
using Application.Features.Supplements.AssignSupplement;
using Application.Features.Supplements.DeactivateSupplementAssignment;
using Application.Features.Supplements.GetSupplementAssignment;
using Application.Features.Supplements.ListSupplementAssignments;
using Application.Features.Supplements.ReactivateSupplementAssignment;
using Application.Features.Supplements.UpdateSupplementAssignment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>Expõe as atribuições de suplementos aos clientes do tenant.</summary>
[Route("api/v1/supplement-assignments")]
[Authorize(ApiPolicyNames.Trainer)]
[SensitiveResponse]
public sealed class ClientSupplementAssignmentsController : ApiControllerBase
{
    /// <summary>Atribui um suplemento a um cliente do tenant.</summary>
    [HttpPost]
    public Task<IActionResult> AssignAsync(
        [FromBody] AssignSupplementRequest request,
        [FromServices] AssignSupplementHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return RespondCreatedAsync(
            handler.HandleAsync(
                new AssignSupplementCommand(
                    request.ClientId,
                    request.SupplementId,
                    request.ServingSize,
                    request.Timing,
                    request.TrainerNotes),
                cancellationToken),
            ClientSupplementAssignmentResponse.From,
            assignment => $"/api/v1/supplement-assignments/{assignment.Id}");
    }

    /// <summary>Lista uma página de atribuições, opcionalmente por cliente.</summary>
    [HttpGet]
    public Task<IActionResult> ListAsync(
        [FromQuery(Name = "client_id")] Guid? clientId,
        [FromQuery(Name = "activity")] SupplementAssignmentActivityFilter activity,
        [FromQuery] PageParameters pageParameters,
        [FromServices] ListSupplementAssignmentsHandler handler,
        CancellationToken cancellationToken)
    {
        var page = pageParameters.EffectivePageNumber;
        var size = pageParameters.EffectivePageSize;

        return RespondAsync(
            handler.HandleAsync(
                new ListSupplementAssignmentsQuery(
                    clientId,
                    activity,
                    page,
                    size),
                cancellationToken),
            result => PagedResponse<ClientSupplementAssignmentResponse>.From(
                result,
                page,
                size,
                ClientSupplementAssignmentResponse.From));
    }

    /// <summary>Devolve uma atribuição do tenant efetivo.</summary>
    [HttpGet("{assignmentId:guid}")]
    public Task<IActionResult> GetAsync(
        Guid assignmentId,
        [FromServices] GetSupplementAssignmentHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(
                new GetSupplementAssignmentQuery(
                    assignmentId),
                    cancellationToken),
            ClientSupplementAssignmentResponse.From);

    /// <summary>Ajusta a prescrição de uma atribuição existente.</summary>
    [HttpPatch("{assignmentId:guid}")]
    public Task<IActionResult> UpdateAsync(
        Guid assignmentId,
        [FromBody] UpdateSupplementAssignmentRequest request,
        [FromServices] UpdateSupplementAssignmentHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return RespondAsync(
            handler.HandleAsync(
                new UpdateSupplementAssignmentCommand(
                    assignmentId,
                    request.ServingSize,
                    request.Timing,
                    request.TrainerNotes),
                cancellationToken),
            ClientSupplementAssignmentResponse.From);
    }

    /// <summary>Desativa uma atribuição sem a eliminar.</summary>
    [HttpPost("{assignmentId:guid}/deactivate")]
    public Task<IActionResult> DeactivateAsync(
        Guid assignmentId,
        [FromServices] DeactivateSupplementAssignmentHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(
                new DeactivateSupplementAssignmentCommand(
                    assignmentId),
                    cancellationToken),
            ClientSupplementAssignmentResponse.From);

    /// <summary>Reativa uma atribuição desativada.</summary>
    [HttpPost("{assignmentId:guid}/reactivate")]
    public Task<IActionResult> ReactivateAsync(
        Guid assignmentId,
        [FromServices] ReactivateSupplementAssignmentHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(
                new ReactivateSupplementAssignmentCommand(
                    assignmentId),
                    cancellationToken),
            ClientSupplementAssignmentResponse.From);
}
