using Api.Authorization;
using Api.Configuration;
using Api.Contracts.Common;
using Api.Contracts.Sessions;
using Application.Features.Sessions.CancelSessionByClient;
using Application.Features.Sessions.CancelSessionByTrainer;
using Application.Features.Sessions.ChangeSessionPack;
using Application.Features.Sessions.CompleteSession;
using Application.Features.Sessions.CreateSession;
using Application.Features.Sessions.GetSession;
using Application.Features.Sessions.ListSessions;
using Application.Features.Sessions.MarkSessionNoShow;
using Application.Features.Sessions.RescheduleSession;
using Application.Features.Sessions.RestoreSession;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>Expõe a agenda de sessões do personal trainer autenticado.</summary>
[Route("api/v1/sessions")]
[Authorize(ApiPolicyNames.Trainer)]
[SensitiveResponse]
public sealed class SessionsController : ApiControllerBase
{
    /// <summary>Agenda uma sessão para um cliente de tenant.</summary>
    [HttpPost]
    public Task<IActionResult> CreateAsync(
        [FromBody] CreateSessionRequest request,
        [FromServices] CreateSessionHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return RespondCreatedAsync(
            handler.HandleAsync(
                new CreateSessionCommand(
                    request.ClientId,
                    request.ClientSessionPackId,
                    request.StartsAt,
                    request.DurationMinutes,
                    request.Location,
                    request.SessionType,
                    request.Notes),
                cancellationToken),
            SessionResponse.From,
            session => $"/api/v1/sessions/{session.Id}");
    }

    /// <summary>Lista uma página de sessões, com filtros de cliente, estado e janela.</summary>
    [HttpGet]
    public Task<IActionResult> ListAsync(
        [FromQuery(Name = "client_id")] Guid? clientId,
        [FromQuery(Name = "status")] SessionStatusFilter? status,
        [FromQuery(Name = "starts_from")] DateTimeOffset? startsFrom,
        [FromQuery(Name = "starts_before")] DateTimeOffset? startsBefore,
        [FromQuery] PageParameters pageParameters,
        [FromServices] ListSessionsHandler handler,
        CancellationToken cancellationToken)
    {
        var page = pageParameters.EffectivePageNumber;
        var size = pageParameters.EffectivePageSize;

        return RespondAsync(
            handler.HandleAsync(
                new ListSessionsQuery(
                    clientId,
                    status,
                    startsFrom,
                    startsBefore,
                    page,
                    size),
                cancellationToken),
            result => PagedResponse<SessionResponse>.From(
                result,
                page,
                size,
                SessionResponse.From));
    }

    /// <summary>Devolve uma sessão do tenant efetivo.</summary>
    [HttpGet("{sessionId:guid}")]
    public Task<IActionResult> GetAsync(
        Guid sessionId,
        [FromServices] GetSessionHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(
                new GetSessionQuery(sessionId),
                cancellationToken),
            SessionResponse.From);

    /// <summary>Move uma sessão agendada para outro instante.</summary>
    [HttpPatch("{sessionId:guid}/reschedule")]
    public Task<IActionResult> RescheduleAsync(
        Guid sessionId,
        [FromBody] RescheduleSessionRequest request,
        [FromServices] RescheduleSessionHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return RespondAsync(
            handler.HandleAsync(
                new RescheduleSessionCommand(
                    sessionId,
                    request.StartsAt,
                    request.DurationMinutes,
                    request.Location),
                cancellationToken),
            SessionResponse.From);
    }

    /// <summary>Marca a sessão como realizada, consumindo um pack associado.</summary>
    [HttpPost("{sessionId:guid}/complete")]
    public Task<IActionResult> CompleteAsync(
        Guid sessionId,
        [FromServices] CompleteSessionHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(
                new CompleteSessionCommand(sessionId),
                cancellationToken),
            SessionResponse.From);

    /// <summary>Regista um cancelamento decidido pelo personal trainer.</summary>
    [HttpPost("{sessionId:guid}/cancel-by-trainer")]
    public Task<IActionResult> CancelByTrainerAsync(
        Guid sessionId,
        [FromServices] CancelSessionByTrainerHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(
                new CancelSessionByTrainerCommand(sessionId),
                cancellationToken),
            SessionResponse.From);

    /// <summary>Regista um cancelamento iniciado pelo cliente.</summary>
    [HttpPost("{sessionId:guid}/cancel-by-client")]
    public Task<IActionResult> CancelByClientAsync(
        Guid sessionId,
        [FromServices] CancelSessionByClientHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(
                new CancelSessionByClientCommand(sessionId),
                cancellationToken),
            SessionResponse.From);

    /// <summary>Marca uma sessão como falta do cliente.</summary>
    [HttpPost("{sessionId:guid}/no-show")]
    public Task<IActionResult> MarkNoShowAsync(
        Guid sessionId,
        [FromServices] MarkSessionNoShowHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(
                new MarkSessionNoShowCommand(sessionId),
                cancellationToken),
            SessionResponse.From);

    /// <summary>Devolve uma sessão cancelada ou marcada como falta ao estado agendado.</summary>
    [HttpPost("{sessionId:guid}/restore")]
    public Task<IActionResult> RestoreAsync(
        Guid sessionId,
        [FromServices] RestoreSessionHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(
                new RestoreSessionCommand(sessionId),
                cancellationToken),
            SessionResponse.From);

    /// <summary>Associa a sessão a outro pack, ou a nenhum quando nulo.</summary>
    [HttpPatch("{sessionId:guid}/pack")]
    public Task<IActionResult> ChangePackAsync(
        Guid sessionId,
        [FromBody] ChangeSessionPackRequest request,
        [FromServices] ChangeSessionPackHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return RespondAsync(
            handler.HandleAsync(
                new ChangeSessionPackCommand(
                    sessionId,
                    request.ClientSessionPackId),
                cancellationToken),
            SessionResponse.From);
    }
}
