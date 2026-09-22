using Api.Authorization;
using Api.Configuration;
using Api.Contracts.Administration;
using Api.Contracts.Common;
using Api.Http;
using Api.Security;
using Application.Features.Administration.ContentModeration.BlockExercise;
using Application.Features.Administration.ContentModeration.BlockFood;
using Application.Features.Administration.ContentModeration.UnblockExercise;
using Application.Features.Administration.ContentModeration.UnblockFood;
using Application.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Api.Controllers;

/// <summary>Expõe apenas Block e Unblock auditados para catálogos privados.</summary>
[ApiController]
[Route("api/v1/admin/content-moderation")]
[Authorize(ApiPolicyNames.AdministrativeContext)]
[AdministrativeContext]
[EnableRateLimiting(ApiRateLimitPolicyNames.Moderation)]
[ProducesResponseType<ApiProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ApiProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ApiProblemDetails>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ApiProblemDetails>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ApiProblemDetails>(StatusCodes.Status409Conflict)]
[ProducesResponseType<ApiProblemDetails>(StatusCodes.Status429TooManyRequests)]
[ProducesResponseType<ApiProblemDetails>(StatusCodes.Status500InternalServerError)]
public sealed class AdminContentModerationController : ControllerBase
{
    private readonly ILogger<AdminContentModerationController> _logger;

    public AdminContentModerationController(ILogger<AdminContentModerationController> logger) =>
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    [HttpPost("foods/{foodId:guid}/block")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<IActionResult> BlockFoodAsync(
        Guid foodId,
        [FromBody] BlockContentRequest request,
        [FromServices] BlockFoodHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync("block", "food", foodId, handler.HandleAsync(
            new BlockFoodCommand(foodId, request.ReasonCode), cancellationToken));

    [HttpPost("foods/{foodId:guid}/unblock")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<IActionResult> UnblockFoodAsync(
        Guid foodId,
        [FromServices] UnblockFoodHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync("unblock", "food", foodId, handler.HandleAsync(
            new UnblockFoodCommand(foodId), cancellationToken));

    [HttpPost("exercises/{exerciseId:guid}/block")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<IActionResult> BlockExerciseAsync(
        Guid exerciseId,
        [FromBody] BlockContentRequest request,
        [FromServices] BlockExerciseHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync("block", "exercise", exerciseId, handler.HandleAsync(
            new BlockExerciseCommand(exerciseId, request.ReasonCode), cancellationToken));

    [HttpPost("exercises/{exerciseId:guid}/unblock")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<IActionResult> UnblockExerciseAsync(
        Guid exerciseId,
        [FromServices] UnblockExerciseHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync("unblock", "exercise", exerciseId, handler.HandleAsync(
            new UnblockExerciseCommand(exerciseId), cancellationToken));

    private async Task<IActionResult> RespondAsync(
        string moderationOperation,
        string contentType,
        Guid contentId,
        Task<Result> operation)
    {
        var result = await operation;
        if (result.IsSuccess)
        {
            _logger.LogInformation(SecurityLogEvents.Moderation,
                "Administrative moderation {ModerationOperation} completed with outcome {SecurityOutcome} for {ContentType} {ContentId}.",
                moderationOperation, "succeeded", contentType, contentId);
        }
        else
        {
            _logger.LogWarning(SecurityLogEvents.Moderation,
                "Administrative moderation {ModerationOperation} completed with outcome {SecurityOutcome} for {ContentType} {ContentId} and error {ErrorCode}.",
                moderationOperation, "rejected", contentType, contentId, result.Error!.Code);
        }

        return result.IsSuccess ? NoContent() : ApiResultMapper.ToProblem(this, result.Error!);
    }
}
