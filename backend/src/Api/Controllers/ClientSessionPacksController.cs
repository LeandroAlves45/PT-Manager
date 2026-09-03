using Api.Authorization;
using Api.Configuration;
using Api.Contracts.Common;
using Api.Contracts.Packs;
using Application.Features.Packs.ClientSessionPacks.AssignClientSessionPack;
using Application.Features.Packs.ClientSessionPacks.CancelClientSessionPack;
using Application.Features.Packs.ClientSessionPacks.GetClientSessionPack;
using Application.Features.Packs.ClientSessionPacks.ListClientSessionPacks;
using Application.Features.Packs.ClientSessionPacks.ListUsableClientSessionPacks;
using Application.Features.Packs.ClientSessionPacks.UpdateClientSessionPackExpectedEndDate;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>Expõe os packs de sessões atribuídos aos clientes do tenant.</summary>
[Route("api/v1/client-session-packs")]
[Authorize(ApiPolicyNames.Trainer)]
[SensitiveResponse]
public sealed class ClientSessionPacksController : ApiControllerBase
{
    /// <summary>Atribui um pack a um cliente do tenant.</summary>
    [HttpPost]
    public Task<IActionResult> AssignAsync(
        [FromBody] AssignClientSessionPackRequest request,
        [FromServices] AssignClientSessionPackHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return RespondCreatedAsync(
            handler.HandleAsync(
                new AssignClientSessionPackCommand(
                    request.ClientId,
                    request.PackTypeId,
                    request.PurchaseDate,
                    request.ExpectedEndDate),
                cancellationToken),
            ClientSessionPackResponse.From,
            pack => $"/api/v1/client-session-packs/{pack.Id}");
    }

    /// <summary>Lista uma página de packs atribuídos, opcionalmente por cliente.</summary>
    [HttpGet]
    public Task<IActionResult> ListAsync(
        [FromQuery(Name = "client_id")] Guid? clientId,
        [FromQuery(Name = "activity")] ClientSessionPackActivityFilter activity,
        [FromQuery] PageParameters pageParameters,
        [FromServices] ListClientSessionPacksHandler handler,
        CancellationToken cancellationToken)
    {
        var page = pageParameters.EffectivePageNumber;
        var size = pageParameters.EffectivePageSize;

        return RespondAsync(
            handler.HandleAsync(
                new ListClientSessionPacksQuery(
                    clientId,
                    activity,
                    page,
                    size),
                cancellationToken),
            result => PagedResponse<ClientSessionPackResponse>.From(
                result, page, size, ClientSessionPackResponse.From));
    }

    /// <summary>Lista os packs de um cliente com saldo utilizável, sem paginação.</summary>
    [HttpGet("usable")]
    public Task<IActionResult> ListUsableAsync(
        [FromQuery(Name = "client_id")] Guid clientId,
        [FromServices] ListUsableClientSessionPacksHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(
                new ListUsableClientSessionPacksQuery(
                    clientId),
                    cancellationToken),
            packs => packs.Select(
                ClientSessionPackResponse.From).ToArray());

    /// <summary>Devolve um pack atribuído do tenant.</summary>
    [HttpGet("{clientSessionPackId:guid}")]
    public Task<IActionResult> GetAsync(
        Guid clientSessionPackId,
        [FromServices] GetClientSessionPackHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(
                new GetClientSessionPackQuery(
                    clientSessionPackId),
                    cancellationToken),
            ClientSessionPackResponse.From);

    /// <summary>Ajusta a data prevista de conclusão do pack.</summary>
    [HttpPatch("{clientSessionPackId:guid}/expected-end-date")]
    public Task<IActionResult> UpdateExpectedEndDateAsync(
        Guid clientSessionPackId,
        [FromBody] UpdateClientSessionPackExpectedEndDateRequest request,
        [FromServices] UpdateClientSessionPackExpectedEndDateHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return RespondAsync(
            handler.HandleAsync(
                new UpdateClientSessionPackExpectedEndDateCommand(
                    clientSessionPackId,
                    request.ExpectedEndDate),
                cancellationToken),
            ClientSessionPackResponse.From);
    }

    /// <summary>Cancela um pack atribuído.</summary>
    [HttpPost("{clientSessionPackId:guid}/cancel")]
    public Task<IActionResult> CancelAsync(
        Guid clientSessionPackId,
        [FromServices] CancelClientSessionPackHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(
                new CancelClientSessionPackCommand(
                    clientSessionPackId),
                    cancellationToken));
}
