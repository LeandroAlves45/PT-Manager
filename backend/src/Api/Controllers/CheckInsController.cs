using Api.Authorization;
using Api.Configuration;
using Api.Contracts.Assessments;
using Api.Contracts.Common;
using Application.Features.Assessments.CheckIns.CancelCheckIn;
using Application.Features.Assessments.CheckIns.CorrectCheckIn;
using Application.Features.Assessments.CheckIns.CreateCheckIn;
using Application.Features.Assessments.CheckIns.GetCheckIn;
using Application.Features.Assessments.CheckIns.ListCheckIns;
using Application.Features.Assessments.CheckIns.RescheduleCheckIn;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>Expõe a gestão de check-ins do personal trainer autenticado.</summary>
[Route("api/v1/check-ins")]
[Authorize(ApiPolicyNames.Trainer)]
[SensitiveResponse]
public sealed class CheckInsController : ApiControllerBase
{
    /// <summary>Agenda um check-in para um cliente do tenant.</summary>
    [HttpPost]
    public Task<IActionResult> CreateAsync(
        [FromBody] CreateCheckInRequest request,
        [FromServices] CreateCheckInHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return RespondCreatedAsync(
            handler.HandleAsync(
                new CreateCheckInCommand(
                    request.ClientId, request.CheckInDate, request.TargetDate),
                cancellationToken),
            CheckInResponse.From,
            checkIn => $"/api/v1/check-ins/{checkIn.Id}");
    }

    /// <summary>Lista uma página de check-ins, com filtros de cliente, estado e janela.</summary>
    [HttpGet]
    public Task<IActionResult> ListAsync(
        [FromQuery(Name = "client_id")] Guid? clientId,
        [FromQuery(Name = "status")] CheckInStatusFilter? status,
        [FromQuery(Name = "from_date")] DateOnly? fromDate,
        [FromQuery(Name = "to_date")] DateOnly? toDate,
        [FromQuery] PageParameters pageParameters,
        [FromServices] ListCheckInsHandler handler,
        CancellationToken cancellationToken)
    {
        var page = pageParameters.EffectivePageNumber;
        var size = pageParameters.EffectivePageSize;

        return RespondAsync(
            handler.HandleAsync(
                new ListCheckInsQuery(clientId, status, fromDate, toDate, page, size),
                cancellationToken),
            result => PagedResponse<CheckInResponse>.From(
                result, page, size, CheckInResponse.From));
    }

    /// <summary>Devolve um check-in do tenant efetivo.</summary>
    [HttpGet("{checkInId:guid}")]
    public Task<IActionResult> GetAsync(
        Guid checkInId,
        [FromServices] GetCheckInHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(new GetCheckInQuery(checkInId), cancellationToken),
            CheckInResponse.From);

    /// <summary>Move um check-in agendado para outra data.</summary>
    [HttpPatch("{checkInId:guid}/reschedule")]
    public Task<IActionResult> RescheduleAsync(
        Guid checkInId,
        [FromBody] RescheduleCheckInRequest request,
        [FromServices] RescheduleCheckInHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return RespondAsync(
            handler.HandleAsync(
                new RescheduleCheckInCommand(
                    checkInId, request.CheckInDate, request.TargetDate),
                cancellationToken),
            CheckInResponse.From);
    }

    /// <summary>Corrige os valores de um check-in já respondido.</summary>
    [HttpPut("{checkInId:guid}/answer")]
    public Task<IActionResult> CorrectAsync(
        Guid checkInId,
        [FromBody] CorrectCheckInRequest request,
        [FromServices] CorrectCheckInHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return RespondAsync(
            handler.HandleAsync(
                new CorrectCheckInCommand(
                    checkInId,
                    request.TargetDate,
                    request.WeightKg,
                    request.BodyFatPercentage,
                    request.Notes,
                    request.BodyMeasurements?.ToInput(),
                    request.Feedback?.ToInput(),
                    request.TrainingAdherenceScore,
                    request.NutritionAdherenceScore),
                cancellationToken),
            CheckInResponse.From);
    }

    /// <summary>Cancela um check-in agendado.</summary>
    [HttpPost("{checkInId:guid}/cancel")]
    public Task<IActionResult> CancelAsync(
        Guid checkInId,
        [FromServices] CancelCheckInHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(new CancelCheckInCommand(checkInId), cancellationToken),
            CheckInResponse.From);
}
