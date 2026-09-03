using Api.Authorization;
using Api.Configuration;
using Api.Contracts.Common;
using Api.Contracts.Supplements;
using Application.Features.Supplements.ArchiveSupplement;
using Application.Features.Supplements.CreateSupplement;
using Application.Features.Supplements.GetSupplement;
using Application.Features.Supplements.ListSupplements;
using Application.Features.Supplements.ReactivateSupplement;
using Application.Features.Supplements.UpdateSupplement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>Expõe o catálogo de suplementos visível ao personal trainer autenticado.</summary>
[Route("api/v1/supplements")]
[Authorize(ApiPolicyNames.Trainer)]
[SensitiveResponse]
public sealed class SupplementsController : ApiControllerBase
{
    /// <summary>Cria um suplemento privado no tenant efetivo.</summary>
    [HttpPost]
    public Task<IActionResult> CreateAsync(
        [FromBody] CreateSupplementRequest request,
        [FromServices] CreateSupplementHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return RespondCreatedAsync(
            handler.HandleAsync(
                new CreateSupplementCommand(
                    request.Name,
                    request.Description,
                    request.UnitOfMeasure,
                    request.ServingSize,
                    request.Timing,
                    request.TrainerNotes),
                cancellationToken),
            SupplementResponse.From,
            supplement => $"/api/v1/supplements/{supplement.Id}");
    }

    /// <summary>Lista uma página de suplementos globais ativos e privados do tenant.</summary>
    [HttpGet]
    public Task<IActionResult> ListAsync(
        [FromQuery(Name = "search")] string? search,
        [FromQuery(Name = "activity")] SupplementActivityFilter activity,
        [FromQuery] PageParameters pageParameters,
        [FromServices] ListSupplementsHandler handler,
        CancellationToken cancellationToken)
    {
        var page = pageParameters.EffectivePageNumber;
        var size = pageParameters.EffectivePageSize;

        return RespondAsync(
            handler.HandleAsync(
                new ListSupplementsQuery(search, activity, page, size),
                cancellationToken),
            result => PagedResponse<SupplementResponse>.From(
                result, page, size, SupplementResponse.From));
    }

    /// <summary>Devolve um suplemento visível ao tenant efetivo.</summary>
    [HttpGet("{supplementId:guid}")]
    public Task<IActionResult> GetAsync(
        Guid supplementId,
        [FromServices] GetSupplementHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(new GetSupplementQuery(supplementId), cancellationToken),
            SupplementResponse.From);

    /// <summary>Substitui os campos editáveis de um suplemento privado.</summary>
    [HttpPatch("{supplementId:guid}")]
    public Task<IActionResult> UpdateAsync(
        Guid supplementId,
        [FromBody] UpdateSupplementRequest request,
        [FromServices] UpdateSupplementHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return RespondAsync(
            handler.HandleAsync(
                new UpdateSupplementCommand(
                    supplementId,
                    request.Name,
                    request.Description,
                    request.UnitOfMeasure,
                    request.ServingSize,
                    request.Timing,
                    request.TrainerNotes),
                cancellationToken),
            SupplementResponse.From);
    }

    /// <summary>Arquiva um suplemento privado sem o eliminar.</summary>
    [HttpPost("{supplementId:guid}/archive")]
    public Task<IActionResult> ArchiveAsync(
        Guid supplementId,
        [FromServices] ArchiveSupplementHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(
                new ArchiveSupplementCommand(supplementId), cancellationToken));

    /// <summary>Reativa um suplemento privado arquivado.</summary>
    [HttpPost("{supplementId:guid}/reactivate")]
    public Task<IActionResult> ReactivateAsync(
        Guid supplementId,
        [FromServices] ReactivateSupplementHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(
                new ReactivateSupplementCommand(supplementId), cancellationToken));
}
