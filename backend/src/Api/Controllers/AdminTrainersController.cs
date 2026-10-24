using Api.Authorization;
using Api.Configuration;
using Api.Contracts.Administration;
using Api.Contracts.Common;
using Api.Http;
using Api.Security;
using Application.Features.Administration.Trainers.Dtos;
using Application.Features.Administration.Trainers.ListExemptTrainers;
using Application.Features.Administration.Trainers.LookupTrainer;
using Application.Features.Administration.Trainers.SetBillingExemption;
using Application.Features.Administration.Trainers.TrainerAccess;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Api.Controllers;

/// <summary>
/// Treinadores vistos pelo superuser: procura por email, isentos, isenção e bloqueio da conta
/// (RN-008, RN-013, RN-018).
/// </summary>
[Route("api/v1/admin/trainers")]
[Authorize(ApiPolicyNames.AdministrativeContext)]
[AdministrativeContext]
[EnableRateLimiting(ApiRateLimitPolicyNames.Moderation)]
[SensitiveResponse]
public sealed class AdminTrainersController : ApiControllerBase
{
    // POST e não GET: o email viaja no corpo para não ficar nos logs de acesso do URL.
    [HttpPost("lookup")]
    [ProducesResponseType<TrainerLookupResponse>(StatusCodes.Status200OK)]
    public Task<IActionResult> LookupAsync(
        [FromBody] TrainerLookupRequest request,
        [FromServices] LookupTrainerHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return RespondAsync(
            handler.HandleAsync(new LookupTrainerQuery(request.Email), cancellationToken),
            TrainerLookupResponse.From);
    }

    [HttpGet("exempt")]
    [ProducesResponseType<PagedResponse<AdminTrainerResponse>>(StatusCodes.Status200OK)]
    public Task<IActionResult> ListExemptAsync(
        [FromQuery] PageParameters pageParameters,
        [FromServices] ListExemptTrainersHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pageParameters);

        var page = pageParameters.GetEffectivePageNumber();
        var size = pageParameters.GetEffectivePageSize();
        return RespondAsync(
            handler.HandleAsync(new ListExemptTrainersQuery(page, size), cancellationToken),
            result => PagedResponse<AdminTrainerResponse>.From(
                result, page, size, AdminTrainerResponse.From));
    }

    [HttpPost("{trainerId:guid}/billing-exemption")]
    [ProducesResponseType<AdminTrainerResponse>(StatusCodes.Status200OK)]
    public Task<IActionResult> GrantExemptionAsync(
        Guid trainerId,
        [FromServices] SetBillingExemptionHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(new SetBillingExemptionCommand(trainerId, true), cancellationToken),
            (AdminTrainerDto trainer) => AdminTrainerResponse.From(trainer));

    [HttpDelete("{trainerId:guid}/billing-exemption")]
    [ProducesResponseType<AdminTrainerResponse>(StatusCodes.Status200OK)]
    public Task<IActionResult> RemoveExemptionAsync(
        Guid trainerId,
        [FromServices] SetBillingExemptionHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(new SetBillingExemptionCommand(trainerId, false), cancellationToken),
            (AdminTrainerDto trainer) => AdminTrainerResponse.From(trainer));

    [HttpPost("{trainerId:guid}/access-block")]
    [ProducesResponseType<AdminTrainerResponse>(StatusCodes.Status200OK)]
    public Task<IActionResult> BlockAccessAsync(
        Guid trainerId,
        [FromBody] BlockTrainerRequest request,
        [FromServices] BlockTrainerHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return RespondAsync(
            handler.HandleAsync(
                new BlockTrainerCommand(trainerId, request.ReasonCode, request.Note),
                cancellationToken),
            (AdminTrainerDto trainer) => AdminTrainerResponse.From(trainer));
    }

    [HttpDelete("{trainerId:guid}/access-block")]
    [ProducesResponseType<AdminTrainerResponse>(StatusCodes.Status200OK)]
    public Task<IActionResult> UnblockAccessAsync(
        Guid trainerId,
        [FromServices] UnblockTrainerHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(new UnblockTrainerCommand(trainerId), cancellationToken),
            (AdminTrainerDto trainer) => AdminTrainerResponse.From(trainer));
}
