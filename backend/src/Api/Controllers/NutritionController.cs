using Api.Authorization;
using Api.Contracts.Nutrition;
using Application.Features.Nutrition.PreviewNutrition;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>
/// Expõe o cálculo nutricional sem efeitos em estado persistido.
/// Usado pelos personal trainers apesar de no handler não valida o tenant, foi documentado.
/// </summary>
[Route("api/v1/nutrition")]
[Authorize(ApiPolicyNames.Trainer)]
public sealed class NutritionController : ApiControllerBase
{
    /// <summary>Calcula energia e macronutrientes sem persistência.</summary>
    [HttpPost("preview")]
    public Task<IActionResult> PreviewAsync(
        [FromBody] PreviewNutritionRequest request,
        [FromServices] PreviewNutritionHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return RespondAsync(
            handler.HandleAsync(
                new PreviewNutritionCommand(request.Calculation.ToInput()),
                cancellationToken),
            NutritionCalculationResponse.From);
    }
}
