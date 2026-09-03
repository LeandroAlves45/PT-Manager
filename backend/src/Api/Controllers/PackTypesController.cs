using Api.Authorization;
using Api.Configuration;
using Api.Contracts.Common;
using Api.Contracts.Packs;
using Application.Features.Packs.PackTypes.ArchivePackType;
using Application.Features.Packs.PackTypes.CreatePackType;
using Application.Features.Packs.PackTypes.GetPackType;
using Application.Features.Packs.PackTypes.ListPackTypes;
using Application.Features.Packs.PackTypes.ReactivatePackType;
using Application.Features.Packs.PackTypes.UpdatePackType;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>Expõe o catálogo de tipos de pack do personal trainer.</summary>
[Route("api/v1/pack-types")]
[Authorize(ApiPolicyNames.Trainer)]
[SensitiveResponse]
public sealed class PackTypesController : ApiControllerBase
{
    /// <summary>Cria um tipo de pack no tenant efetivo.</summary>
    [HttpPost]
    public Task<IActionResult> CreateAsync(
        [FromBody] CreatePackTypeRequest request,
        [FromServices] CreatePackTypeHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return RespondCreatedAsync(
            handler.HandleAsync(
                new CreatePackTypeCommand(
                    request.Name,
                    request.SessionCount,
                    request.PriceCents,
                    request.Currency,
                    request.ExpectedDurationDays),
                cancellationToken),
            PackTypeResponse.From,
            packType => $"/api/v1/pack-types/{packType.Id}");
    }

    /// <summary>Lista uma página de tipos de pack do tenant.</summary>
    [HttpGet]
    public Task<IActionResult> ListAsync(
        [FromQuery(Name = "search")] string? search,
        [FromQuery(Name = "activity")] PackTypeActivityFilter activity,
        [FromQuery] PageParameters pageParameters,
        [FromServices] ListPackTypesHandler handler,
        CancellationToken cancellationToken)
    {
        var page = pageParameters.EffectivePageNumber;
        var size = pageParameters.EffectivePageSize;

        return RespondAsync(
            handler.HandleAsync(
                new ListPackTypesQuery(
                    search,
                    activity,
                    page,
                    size),
                cancellationToken),
            result => PagedResponse<PackTypeResponse>.From(
                result,
                page,
                size,
                PackTypeResponse.From));
    }

    /// <summary>Devolve um tipo de pack do tenant efetivo.</summary>
    [HttpGet("{packTypeId:guid}")]
    public Task<IActionResult> GetAsync(
        Guid packTypeId,
        [FromServices] GetPackTypeHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(
                new GetPackTypeQuery(packTypeId),
                cancellationToken),
            PackTypeResponse.From);

    /// <summary>Substitui os campos editáveis de um tipo de pack.</summary>
    [HttpPatch("{packTypeId:guid}")]
    public Task<IActionResult> UpdateAsync(
        Guid packTypeId,
        [FromBody] UpdatePackTypeRequest request,
        [FromServices] UpdatePackTypeHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return RespondAsync(
            handler.HandleAsync(
                new UpdatePackTypeCommand(
                    packTypeId,
                    request.Name,
                    request.SessionCount,
                    request.PriceCents,
                    request.Currency,
                    request.ExpectedDurationDays),
                cancellationToken),
            PackTypeResponse.From);
    }

    /// <summary>Arquiva um tipo de pack, retirando-o da oferta.</summary>
    [HttpPost("{packTypeId:guid}/archive")]
    public Task<IActionResult> ArchiveAsync(
        Guid packTypeId,
        [FromServices] ArchivePackTypeHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(
                new ArchivePackTypeCommand(packTypeId),
                cancellationToken));

    /// <summary>Reativa um tipo de pack que foi arquivado.</summary>
    [HttpPost("{packTypeId:guid}/reactivate")]
    public Task<IActionResult> ReactivateAsync(
        Guid packTypeId,
        [FromServices] ReactivatePackTypeHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(
                new ReactivatePackTypeCommand(packTypeId),
                cancellationToken));
}
