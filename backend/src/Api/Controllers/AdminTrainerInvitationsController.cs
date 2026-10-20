using Api.Authorization;
using Api.Configuration;
using Api.Contracts.Administration;
using Api.Security;
using Application.Features.Administration.Trainers.Dtos;
using Application.Features.Administration.Trainers.InviteTrainer;
using Application.Features.Administration.Trainers.ListTrainerInvitations;
using Application.Features.Administration.Trainers.RevokeTrainerInvitation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Api.Controllers;

/// <summary>Convites de treinador isento enviados pelo superuser (RN-008, FL-005, U51).</summary>
[Route("api/v1/admin/trainer-invitations")]
[Authorize(ApiPolicyNames.AdministrativeContext)]
[AdministrativeContext]
[EnableRateLimiting(ApiRateLimitPolicyNames.Moderation)]
[SensitiveResponse]
public sealed class AdminTrainerInvitationsController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<PendingTrainerInvitationResponse>>(StatusCodes.Status200OK)]
    public Task<IActionResult> ListPendingAsync(
        [FromServices] ListTrainerInvitationsHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(new ListTrainerInvitationsQuery(), cancellationToken),
            (IReadOnlyList<PendingTrainerInvitationDto> items) =>
                items.Select(PendingTrainerInvitationResponse.From).ToArray());

    [HttpPost]
    [ProducesResponseType<PendingTrainerInvitationResponse>(StatusCodes.Status201Created)]
    public Task<IActionResult> InviteAsync(
        [FromBody] InviteTrainerRequest request,
        [FromServices] InviteTrainerHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return RespondCreatedAsync(
            handler.HandleAsync(new InviteTrainerCommand(request.Email), cancellationToken),
            PendingTrainerInvitationResponse.From,
            invitation => $"/api/v1/admin/trainer-invitations/{invitation.InvitationId}");
    }

    [HttpDelete("{invitationId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<IActionResult> RevokeAsync(
        Guid invitationId,
        [FromServices] RevokeTrainerInvitationHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(handler.HandleAsync(
            new RevokeTrainerInvitationCommand(invitationId),
            cancellationToken));
}
