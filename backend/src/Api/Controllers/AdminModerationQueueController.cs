using Api.Authorization;
using Api.Configuration;
using Api.Contracts.Administration;
using Api.Contracts.Common;
using Api.Security;
using Application.Features.Administration.ContentModeration.ListModerationQueue;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Api.Controllers;

/// <summary>
/// Fila de moderação: listagens paginadas do conteúdo privado de todos os personal trainers, para o
/// superuser poder bloquear sem conhecer o id. Fica num controller próprio porque precisa do
/// <c>ApiControllerBase</c> (mapeamento de Result para ProblemDetails), enquanto o controller
/// de block/unblock mantém a sua própria resposta auditada.
/// </summary>
[Route("api/v1/admin/content-moderation")]
[Authorize(ApiPolicyNames.AdministrativeContext)]
[AdministrativeContext]
[EnableRateLimiting(ApiRateLimitPolicyNames.Moderation)]
[SensitiveResponse]
public sealed class AdminModerationQueueController : ApiControllerBase
{
    private readonly ILogger<AdminModerationQueueController> _logger;

    public AdminModerationQueueController(ILogger<AdminModerationQueueController> logger) =>
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>Lista alimentos privados de todos os personal trainers.</summary>
    [HttpGet("foods")]
    [ProducesResponseType<PagedResponse<ModerationQueueItemResponse>>(StatusCodes.Status200OK)]
    public Task<IActionResult> ListFoodsAsync(
        [FromQuery(Name = "status")] ModerationStatusFilter status,
        [FromQuery(Name = "search")] string? search,
        [FromQuery] PageParameters pageParameters,
        [FromServices] ListModerationQueueHandler handler,
        CancellationToken cancellationToken) =>
        ListAsync(
            ModerationContentKind.Food,
            status,
            search,
            pageParameters,
            handler,
            cancellationToken);

    /// <summary>Lista exercícios privados de todos os personal trainers.</summary>
    [HttpGet("exercises")]
    [ProducesResponseType<PagedResponse<ModerationQueueItemResponse>>(StatusCodes.Status200OK)]
    public Task<IActionResult> ListExercisesAsync(
        [FromQuery(Name = "status")] ModerationStatusFilter status,
        [FromQuery(Name = "search")] string? search,
        [FromQuery] PageParameters pageParameters,
        [FromServices] ListModerationQueueHandler handler,
        CancellationToken cancellationToken) =>
        ListAsync(
            ModerationContentKind.Exercise,
            status,
            search,
            pageParameters,
            handler,
            cancellationToken);

    private Task<IActionResult> ListAsync(
        ModerationContentKind kind,
        ModerationStatusFilter status,
        string? search,
        PageParameters pageParameters,
        ListModerationQueueHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pageParameters);
        ArgumentNullException.ThrowIfNull(handler);

        var page = pageParameters.GetEffectivePageNumber();
        var size = pageParameters.GetEffectivePageSize();

        _logger.LogInformation(
            SecurityLogEvents.Moderation,
            "Administrative moderation queue read requested for {ContentType} with status {ModerationStatus}.",
            kind.ToString(),
            status.ToString());

        return RespondAsync(
            handler.HandleAsync(
                new ListModerationQueueQuery(kind, status, search, page, size),
                cancellationToken),
            result => PagedResponse<ModerationQueueItemResponse>.From(
                result,
                page,
                size,
                ModerationQueueItemResponse.From));
    }
}
