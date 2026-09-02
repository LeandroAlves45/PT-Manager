using Api.Authorization;
using Api.Configuration;
using Api.Contracts.Common;
using Api.Contracts.Nutrition;
using Application.Features.Nutrition.Foods.ArchiveFood;
using Application.Features.Nutrition.Foods.CreateFood;
using Application.Features.Nutrition.Foods.GetFood;
using Application.Features.Nutrition.Foods.ListFoods;
using Application.Features.Nutrition.Foods.ReactivateFood;
using Application.Features.Nutrition.Foods.UpdateFood;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>Expõe o catálogo de alimentos visíveis ao personal trainer.</summary>
[Route("api/v1/foods")]
[Authorize(ApiPolicyNames.Trainer)]
[SensitiveResponse]
public sealed class FoodsController : ApiControllerBase
{
    /// <summary>Cria um alimento privado no tenant efetivo.</summary>
    [HttpPost]
    public Task<IActionResult> CreateAsync(
        [FromBody] CreateFoodRequest request,
        [FromServices] CreateFoodHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return RespondCreatedAsync(
            handler.HandleAsync(
                new CreateFoodCommand(
                    request.Name,
                    request.Description,
                    request.Protein,
                    request.Carbs,
                    request.Fats,
                    request.Fiber),
                cancellationToken),
            FoodResponse.From,
            food => $"/api/v1/foods/{food.Id}");
    }

    /// <summary>Lista uma página de alimentos globais ativos e privados do tenant.</summary>
    [HttpGet]
    public Task<IActionResult> ListAsync(
        [FromQuery(Name = "search")] string? search,
        [FromQuery(Name = "activity")] FoodActivityFilter activity,
        [FromQuery(Name = "page_number")] int pageNumber,
        [FromQuery(Name = "page_size")] int pageSize,
        [FromServices] ListFoodsHandler handler,
        CancellationToken cancellationToken)
    {
        var page = pageNumber <= 0 ? 1 : pageNumber;
        var size = pageSize <= 0 ? 50 : pageSize;

        return RespondAsync(
            handler.HandleAsync(
                new ListFoodsQuery(
                    search,
                    activity,
                    page,
                    size),
                cancellationToken),
            result => PagedResponse<FoodResponse>.From(
                result,
                page,
                size,
                FoodResponse.From)
            );
    }

    /// <summary>Devolve um alimento visível ao tenant efetivo.</summary>
    [HttpGet("{foodId:guid}")]
    public Task<IActionResult> GetAsync(
        Guid foodId,
        [FromServices] GetFoodHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(
                new GetFoodQuery(foodId),
                cancellationToken),
            FoodResponse.From);

    /// <summary>Substitui os campos editáveis de um alimento privado.</summary>
    [HttpPatch("{foodId:guid}")]
    public Task<IActionResult> UpdateAsync(
        Guid foodId,
        [FromBody] UpdateFoodRequest request,
        [FromServices] UpdateFoodHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return RespondAsync(
            handler.HandleAsync(
                new UpdateFoodCommand(
                    foodId,
                    request.Name,
                    request.Description,
                    request.Protein,
                    request.Carbs,
                    request.Fats,
                    request.Fiber),
                cancellationToken),
            FoodResponse.From);
    }

    /// <summary>Arquiva um alimento privado sem o eliminar.</summary>
    [HttpPost("{foodId:guid}/archive")]
    public Task<IActionResult> ArchiveAsync(
        Guid foodId,
        [FromServices] ArchiveFoodHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(
                new ArchiveFoodCommand(foodId),
                cancellationToken));

    /// <summary>Reativa um alimento privado arquivado.</summary>
    [HttpPost("{foodId:guid}/reactivate")]
    public Task<IActionResult> ReactivateAsync(
        Guid foodId,
        [FromServices] ReactivateFoodHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(
                new ReactivateFoodCommand(foodId),
                cancellationToken));
}
