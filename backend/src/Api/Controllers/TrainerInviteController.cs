using Api.Configuration;
using Api.Contracts.Authentication;
using Api.Security;
using Application.Features.Authentication.TrainerInvitations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Api.Controllers;

/// <summary>
/// Página anónima do convite de treinador isento. O token viaja no corpo, nunca na query
/// string, para não ficar em logs de acesso.
/// </summary>
[Route("api/v1/auth/trainer-invite")]
[SensitiveResponse]
[RequireOrigin]
[EnableRateLimiting(ApiRateLimitPolicyNames.InviteRegister)]
public sealed class TrainerInviteController : ApiControllerBase
{
    [HttpPost("status")]
    [ProducesResponseType<TrainerInvitationStatusResponse>(StatusCodes.Status200OK)]
    public Task<IActionResult> GetStatusAsync(
        [FromBody] TrainerInvitationTokenRequest request,
        [FromServices] GetTrainerInvitationStatusHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return RespondAsync(
            handler.HandleAsync(new GetTrainerInvitationStatusQuery(request.Token), cancellationToken),
            TrainerInvitationStatusResponse.From);
    }

    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<IActionResult> RegisterAsync(
        [FromBody] InvitedTrainerRegistrationRequest request,
        [FromServices] RegisterInvitedTrainerHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return RespondAsync(handler.HandleAsync(
            new RegisterInvitedTrainerCommand(
                request.Token,
                request.FullName,
                request.Password,
                request.ConfirmPassword),
            cancellationToken));
    }
}
