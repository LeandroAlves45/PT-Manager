using Api.Authorization;
using Api.Configuration;
using Api.Contracts.Common;
using Api.Contracts.Nutrition;
using Application.Features.Nutrition.Foods.ArchiveGlobalFood;
using Application.Features.Nutrition.Foods.CreateGlobalFood;
using Application.Features.Nutrition.Foods.DeleteGlobalFood;
using Application.Features.Nutrition.Foods.GetGlobalFood;
using Application.Features.Nutrition.Foods.ListGlobalFoods;
using Application.Features.Nutrition.Foods.ReactivateGlobalFood;
using Application.Features.Nutrition.Foods.UpdateGlobalFood;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>Expõe a curadoria do catálogo global de alimentos visível ao superuser.</summary>
[Route("api/v1/global-foods")]
[Authorize(ApiPolicyNames.Superuser)]
[AdministrativeContext]
[SensitiveResponse]
public sealed class GlobalFoodsController : ApiControllerBase
{
    /// <summary>Cria um alimento no catálogo global.</summary>
    [HttpPost]
    public Task<IActionResult> CreateAsync(
        [FromBody] CreateGlobalFoodRequest request,
        [FromServices] CreateGlobalFoodHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return RespondCreatedAsync(
            handler.HandleAsync(
                new CreateGlobalFoodCommand(
                    request.Name,
                    request.Description,
                    request.Protein,
                    request.Carbs,
                    request.Fats,
                    request.Fiber),
                cancellationToken),
            GlobalFoodResponse.From,
            food => $"/api/v1/global-foods/{food.Id}");
    }

    /// <summary>Lista uma página de alimentos globais.</summary>
    [HttpGet]
    public Task<IActionResult> ListAsync(
        [FromQuery(Name = "search")] string? search,
        [FromQuery(Name = "activity")] GlobalFoodActivityFilter activity,
        [FromQuery] PageParameters pageParameters,
        [FromServices] ListGlobalFoodsHandler handler,
        CancellationToken cancellationToken)
    {
        var page = pageParameters.EffectivePageNumber;
        var size = pageParameters.EffectivePageSize;

        return RespondAsync(
            handler.HandleAsync(
                new ListGlobalFoodsQuery(
                    search,
                    activity,
                    page,
                    size),
                cancellationToken),
            result => PagedResponse<GlobalFoodResponse>.From(
                result,
                page,
                size,
                GlobalFoodResponse.From)
            );
    }

    /// <summary>Devolve um alimento global.</summary>
    [HttpGet("{foodId:guid}")]
    public Task<IActionResult> GetAsync(
        Guid foodId,
        [FromServices] GetGlobalFoodHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(
                new GetGlobalFoodQuery(foodId),
                cancellationToken),
            GlobalFoodResponse.From);

    /// <summary>Substitui os campos editáveis de um alimento global.</summary>
    [HttpPatch("{foodId:guid}")]
    public Task<IActionResult> UpdateAsync(
        Guid foodId,
        [FromBody] UpdateGlobalFoodRequest request,
        [FromServices] UpdateGlobalFoodHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return RespondAsync(
            handler.HandleAsync(
                new UpdateGlobalFoodCommand(
                    foodId,
                    request.Name,
                    request.Description,
                    request.Protein,
                    request.Carbs,
                    request.Fats,
                    request.Fiber),
                cancellationToken),
            GlobalFoodResponse.From);
    }

    /// <summary>Arquiva um alimento global, retirando-o dos catálogos ativos.</summary>
    [HttpPost("{foodId:guid}/archive")]
    public Task<IActionResult> ArchiveAsync(
        Guid foodId,
        [FromServices] ArchiveGlobalFoodHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(
                new ArchiveGlobalFoodCommand(foodId),
                cancellationToken));

    /// <summary>Reativa um alimento global arquivado.</summary>
    [HttpPost("{foodId:guid}/reactivate")]
    public Task<IActionResult> ReactivateAsync(
        Guid foodId,
        [FromServices] ReactivateGlobalFoodHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(
                new ReactivateGlobalFoodCommand(foodId),
                cancellationToken));

    /// <summary>Elimina definitivamente um alimento global sem referências.</summary>
    [HttpDelete("{foodId:guid}")]
    public Task<IActionResult> DeleteAsync(
        Guid foodId,
        [FromServices] DeleteGlobalFoodHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(
                new DeleteGlobalFoodCommand(foodId),
                cancellationToken));
}
