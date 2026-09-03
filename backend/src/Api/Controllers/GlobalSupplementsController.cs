using Api.Authorization;
using Api.Configuration;
using Api.Contracts.Common;
using Api.Contracts.Supplements;
using Application.Features.Supplements.ArchiveGlobalSupplement;
using Application.Features.Supplements.CreateGlobalSupplement;
using Application.Features.Supplements.DeleteGlobalSupplement;
using Application.Features.Supplements.GetGlobalSupplement;
using Application.Features.Supplements.ListGlobalSupplements;
using Application.Features.Supplements.ReactivateGlobalSupplement;
using Application.Features.Supplements.UpdateGlobalSupplement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>Expõe a curadoria do catálogo global de suplementos.</summary>
[Route("api/v1/global-supplements")]
[Authorize(ApiPolicyNames.Superuser)]
[AdministrativeContext]
[SensitiveResponse]
public sealed class GlobalSupplementsController : ApiControllerBase
{
    /// <summary>Cria um suplemento no catálogo global.</summary>
    [HttpPost]
    public Task<IActionResult> CreateAsync(
        [FromBody] CreateGlobalSupplementRequest request,
        [FromServices] CreateGlobalSupplementHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return RespondCreatedAsync(
            handler.HandleAsync(
                new CreateGlobalSupplementCommand(
                    request.Name,
                    request.Description,
                    request.UnitOfMeasure,
                    request.ServingSize,
                    request.Timing,
                    request.TrainerNotes),
                cancellationToken),
            GlobalSupplementResponse.From,
            supplement => $"/api/v1/global-supplements/{supplement.Id}");
    }

    /// <summary>Lista uma página do catálogo global.</summary>
    [HttpGet]
    public Task<IActionResult> ListAsync(
        [FromQuery(Name = "search")] string? search,
        [FromQuery(Name = "activity")] GlobalSupplementActivityFilter activity,
        [FromQuery] PageParameters pageParameters,
        [FromServices] ListGlobalSupplementsHandler handler,
        CancellationToken cancellationToken)
    {
        var page = pageParameters.EffectivePageNumber;
        var size = pageParameters.EffectivePageSize;

        return RespondAsync(
            handler.HandleAsync(
                new ListGlobalSupplementsQuery(
                    search,
                    activity,
                    page,
                    size),
                cancellationToken),
            result => PagedResponse<GlobalSupplementResponse>.From(
                result,
                page,
                size,
                GlobalSupplementResponse.From));
    }

    /// <summary>Devolve um suplemento global.</summary>
    [HttpGet("{supplementId:guid}")]
    public Task<IActionResult> GetAsync(
        Guid supplementId,
        [FromServices] GetGlobalSupplementHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(
                new GetGlobalSupplementQuery(
                    supplementId),
                    cancellationToken),
            GlobalSupplementResponse.From);

    /// <summary>Substitui os campos editáveis de um suplemento global.</summary>
    [HttpPatch("{supplementId:guid}")]
    public Task<IActionResult> UpdateAsync(
        Guid supplementId,
        [FromBody] UpdateGlobalSupplementRequest request,
        [FromServices] UpdateGlobalSupplementHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return RespondAsync(
            handler.HandleAsync(
                new UpdateGlobalSupplementCommand(
                    supplementId,
                    request.Name,
                    request.Description,
                    request.UnitOfMeasure,
                    request.ServingSize,
                    request.Timing,
                    request.TrainerNotes),
                cancellationToken),
            GlobalSupplementResponse.From);
    }

    /// <summary>Arquiva um suplemento global.</summary>
    [HttpPost("{supplementId:guid}/archive")]
    public Task<IActionResult> ArchiveAsync(
        Guid supplementId,
        [FromServices] ArchiveGlobalSupplementHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(
                new ArchiveGlobalSupplementCommand(
                    supplementId),
                    cancellationToken));

    /// <summary>Reativa um suplemento global arquivado.</summary>
    [HttpPost("{supplementId:guid}/reactivate")]
    public Task<IActionResult> ReactivateAsync(
        Guid supplementId,
        [FromServices] ReactivateGlobalSupplementHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(
                new ReactivateGlobalSupplementCommand(
                    supplementId),
                    cancellationToken));

    /// <summary>Elimina definitivamente um suplemento global sem referências.</summary>
    [HttpDelete("{supplementId:guid}")]
    public Task<IActionResult> DeleteAsync(
        Guid supplementId,
        [FromServices] DeleteGlobalSupplementHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(
                new DeleteGlobalSupplementCommand(
                    supplementId),
                    cancellationToken));
}
