using Api.Authorization;
using Api.Configuration;
using Api.Contracts.Common;
using Api.Contracts.Nutrition;
using Application.Features.Nutrition.MealPlans.ArchiveMealPlan;
using Application.Features.Nutrition.MealPlans.CreateMealPlan;
using Application.Features.Nutrition.MealPlans.GetMealPlan;
using Application.Features.Nutrition.MealPlans.ListMealPlans;
using Application.Features.Nutrition.MealPlans.ReactivateMealPlan;
using Application.Features.Nutrition.MealPlans.UpdateMealPlan;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>Expõe a prescrição de planos alimentares do personal trainer.</summary>
[Route("api/v1/meal-plans")]
[Authorize(ApiPolicyNames.Trainer)]
[SensitiveResponse]
public sealed class MealPlansController : ApiControllerBase
{
    /// <summary>Cria um plano alimentar e a respetiva árvore inicial.</summary>
    [HttpPost]
    public Task<IActionResult> CreateAsync(
        [FromBody] CreateMealPlanRequest request,
        [FromServices] CreateMealPlanHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return RespondCreatedAsync(
            handler.HandleAsync(
                new CreateMealPlanCommand(
                    request.ClientId,
                    request.Name,
                    request.Description,
                    request.StartsDate,
                    request.EndsDate,
                    request.Calculation.ToInput(),
                    request.Structure.ToInput()),
                cancellationToken),
            MealPlanDetailsResponse.From,
            plan => $"/api/v1/meal-plans/{plan.Id}");
    }

    /// <summary>Lista uma página de planos do tenant, opcionalmente por cliente.</summary>
    [HttpGet]
    public Task<IActionResult> ListAsync(
        [FromQuery(Name = "client_id")] Guid? clientId,
        [FromQuery(Name = "search")] string? search,
        [FromQuery(Name = "activity")] MealPlanActivityFilter activity,
        [FromQuery] PageParameters pageParameters,
        [FromServices] ListMealPlansHandler handler,
        CancellationToken cancellationToken)
    {
        var page = pageParameters.EffectivePageNumber;
        var size = pageParameters.EffectivePageSize;

        return RespondAsync(
            handler.HandleAsync(
                new ListMealPlansQuery(
                    clientId,
                    search,
                    activity,
                    page,
                    size),
                cancellationToken),
            result => PagedResponse<MealPlanSummaryResponse>.From(
                result,
                page,
                size,
                MealPlanSummaryResponse.From)
            );
    }

    /// <summary>Devolve um plano completo com refeições, itens e totais.</summary>
    [HttpGet("{mealPlanId:guid}")]
    public Task<IActionResult> GetAsync(
        Guid mealPlanId,
        [FromServices] GetMealPlanHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(
                new GetMealPlanQuery(mealPlanId),
                cancellationToken),
            MealPlanDetailsResponse.From);

    /// <summary>Reconcilia um plano existente com a estrutura pedida.</summary>
    [HttpPut("{mealPlanId:guid}")]
    public Task<IActionResult> UpdateAsync(
        Guid mealPlanId,
        [FromBody] UpdateMealPlanRequest request,
        [FromServices] UpdateMealPlanHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return RespondAsync(
            handler.HandleAsync(
                new UpdateMealPlanCommand(
                    mealPlanId,
                    request.Name,
                    request.Description,
                    request.StartsDate,
                    request.EndsDate,
                    request.Calculation?.ToInput(),
                    request.Structure.ToInput()),
                cancellationToken),
            MealPlanDetailsResponse.From);
    }

    /// <summary>Arquiva um plano sem o eliminar.</summary>
    [HttpPost("{mealPlanId:guid}/archive")]
    public Task<IActionResult> ArchiveAsync(
        Guid mealPlanId,
        [FromServices] ArchiveMealPlanHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(
                new ArchiveMealPlanCommand(mealPlanId),
                cancellationToken));

    /// <summary>Reativa um plano arquivado.</summary>
    [HttpPost("{mealPlanId:guid}/reactivate")]
    public Task<IActionResult> ReactivateAsync(
        Guid mealPlanId,
        [FromServices] ReactivateMealPlanHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(
                new ReactivateMealPlanCommand(mealPlanId),
                cancellationToken));
}
