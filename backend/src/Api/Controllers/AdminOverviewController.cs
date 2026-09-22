using Api.Authorization;
using Api.Configuration;
using Api.Contracts.Administration;
using Api.Security;
using Application.Features.Administration.Overview;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Api.Controllers;

/// <summary>
/// Visão geral da plataforma para o superuser: apenas contagens de catálogos e de filas.
/// Tal como os restantes GET administrativos, regista um evento de segurança e não cria
/// entrada de auditoria persistida (leituras não são auditadas).
/// </summary>
[Route("api/v1/admin/overview")]
[Authorize(ApiPolicyNames.AdministrativeContext)]
[AdministrativeContext]
[EnableRateLimiting(ApiRateLimitPolicyNames.Moderation)]
public sealed class AdminOverviewController : ApiControllerBase
{
    private readonly ILogger<AdminOverviewController> _logger;

    public AdminOverviewController(ILogger<AdminOverviewController> logger) =>
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>Devolve as contagens dos catálogos globais e das filas privadas.</summary>
    [HttpGet]
    [ProducesResponseType<AdminOverviewResponse>(StatusCodes.Status200OK)]
    public Task<IActionResult> GetAsync(
        [FromServices] GetAdminOverviewHandler handler,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            SecurityLogEvents.Moderation,
            "Administrative overview read requested.");

        return RespondAsync(
            handler.HandleAsync(new GetAdminOverviewQuery(), cancellationToken),
            AdminOverviewResponse.From);
    }
}
