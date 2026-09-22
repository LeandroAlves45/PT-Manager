using Api.Authorization;
using Api.Configuration;
using Api.Contracts.Dashboard;
using Application.Features.Dashboard.GetTrainerDashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>Expõe o Dashboard agregado do personal trainer autenticado.</summary>
[Route("api/v1/dashboard")]
[Authorize(ApiPolicyNames.Trainer)]
[SensitiveResponse]
public sealed class DashboardController : ApiControllerBase
{
    /// <summary>Devolve todos os blocos do painel num só pedido.</summary>
    [HttpGet]
    [ProducesResponseType<TrainerDashboardResponse>(StatusCodes.Status200OK)]
    public Task<IActionResult> GetAsync(
        [FromServices] GetTrainerDashboardHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(new GetTrainerDashboardQuery(), cancellationToken),
            TrainerDashboardResponse.From);
}
