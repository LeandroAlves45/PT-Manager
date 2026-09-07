using Api.Authorization;
using Api.Configuration;
using Api.Contracts.Administration;
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
public sealed class AdminContentModerationController : ControllerBase
{
    private readonly ILogger<AdminContentModerationController> _logger;

    public AdminContentModerationController(ILogger<AdminContentModerationController> logger) =>
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    [HttpPost("foods/{foodId:guid}/block")]
    public Task<IActionResult> BlockFoodAsync(
        Guid foodId,
        [FromBody] BlockContentRequest request,
        [FromServices] BlockFoodHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync("block", "food", foodId, handler.HandleAsync(
            new BlockFoodCommand(foodId, request.ReasonCode), cancellationToken));

    [HttpPost("foods/{foodId:guid}/unblock")]
    public Task<IActionResult> UnblockFoodAsync(
        Guid foodId,
        [FromServices] UnblockFoodHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync("unblock", "food", foodId, handler.HandleAsync(
            new UnblockFoodCommand(foodId), cancellationToken));

    [HttpPost("exercises/{exerciseId:guid}/block")]
    public Task<IActionResult> BlockExerciseAsync(
        Guid exerciseId,
        [FromBody] BlockContentRequest request,
        [FromServices] BlockExerciseHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync("block", "exercise", exerciseId, handler.HandleAsync(
            new BlockExerciseCommand(exerciseId, request.ReasonCode), cancellationToken));

    [HttpPost("exercises/{exerciseId:guid}/unblock")]
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
