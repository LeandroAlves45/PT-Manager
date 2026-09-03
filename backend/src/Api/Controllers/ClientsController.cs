using Api.Authorization;
using Api.Configuration;
using Api.Contracts.Clients;
using Api.Contracts.Common;
using Application.Features.Clients.ArchiveClient;
using Application.Features.Clients.CreateClient;
using Application.Features.Clients.GetClient;
using Application.Features.Clients.ListClients;
using Application.Features.Clients.ReactivateClient;
using Application.Features.Clients.UpdateClient;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>Expõe a gestão de fichas de cliente do personal trainer autenticado.</summary>
[Route("api/v1/clients")]
[Authorize(ApiPolicyNames.Trainer)]
[SensitiveResponse]
public sealed class ClientsController : ApiControllerBase
{
    /// <summary>Cria uma ficha de cliente e devolve o detalhe completo.</summary>
    [HttpPost]
    public Task<IActionResult> CreateAsync(
        [FromBody] CreateClientRequest request,
        [FromServices] CreateClientHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return RespondCreatedAsync(
            handler.HandleAsync(
                new CreateClientCommand(
                    request.Name,
                    request.ContactEmail,
                    request.Phone,
                    request.BirthDate,
                    request.Sex,
                    request.Objective,
                    request.Notes,
                    request.EmergencyContactName,
                    request.EmergencyContactPhone),
                cancellationToken),
            ClientDetailsResponse.From,
            client => $"/api/v1/clients/{client.Id}");
    }

    /// <summary>Lista uma página determinística de fichas do tenant.</summary>
    [HttpGet]
    public Task<IActionResult> ListAsync(
        [FromQuery(Name = "search")] string? search,
        [FromQuery(Name = "activity")] ClientActivityFilter activity,
        [FromQuery] PageParameters pageParameters,
        [FromServices] ListClientsHandler handler,
        CancellationToken cancellationToken)
    {
        var page = pageParameters.EffectivePageNumber;
        var size = pageParameters.EffectivePageSize;

        return RespondAsync(
            handler.HandleAsync(
                new ListClientsQuery(
                    search,
                    activity,
                    page,
                    size
                ),
                cancellationToken
            ),
            result => PagedResponse<ClientSummaryResponse>.From(
                result,
                page,
                size,
                ClientSummaryResponse.From
            ));
    }

    /// <summary>Devolve o detalhe de uma ficha do tenant.</summary>
    [HttpGet("{clientId:guid}")]
    public Task<IActionResult> GetAsync(
        Guid clientId,
        [FromServices] GetClientHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(
                new GetClientQuery(clientId),
                cancellationToken
            ),
            ClientDetailsResponse.From);

    /// <summary>Substitui o perfil editável da ficha do cliente.</summary>
    [HttpPatch("{clientId:guid}")]
    public Task<IActionResult> UpdateAsync(
        Guid clientId,
        [FromBody] UpdateClientRequest request,
        [FromServices] UpdateClientHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return RespondAsync(
            handler.HandleAsync(
                new UpdateClientCommand(
                    clientId,
                    request.Name,
                    request.ContactEmail,
                    request.Phone,
                    request.BirthDate,
                    request.Sex,
                    request.Objective,
                    request.Notes,
                    request.EmergencyContactName,
                    request.EmergencyContactPhone
                ),
                cancellationToken
            ),
            ClientDetailsResponse.From);
    }

    /// <summary>Arquiva o cliente sem a eliminar.</summary>
    [HttpPost("{clientId:guid}/archive")]
    public Task<IActionResult> ArchiveAsync(
        Guid clientId,
        [FromServices] ArchiveClientHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(
                new ArchiveClientCommand(clientId),
                cancellationToken
            ));

    /// <summary>Reativa um cliente arquivado.</summary>
    [HttpPost("{clientId:guid}/reactivate")]
    public Task<IActionResult> ReactivateAsync(
        Guid clientId,
        [FromServices] ReactivateClientHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(
                new ReactivateClientCommand(clientId),
                cancellationToken
            ));
}
