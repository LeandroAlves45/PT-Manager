using Api.Authorization;
using Api.Configuration;
using Api.Contracts.Administration;
using Api.Http;
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
    [HttpPost("foods/{foodId:guid}/block")]
    public Task<IActionResult> BlockFoodAsync(
        Guid foodId,
        [FromBody] BlockContentRequest request,
        [FromServices] BlockFoodHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(handler.HandleAsync(
            new BlockFoodCommand(foodId, request.ReasonCode), cancellationToken));

    [HttpPost("foods/{foodId:guid}/unblock")]
    public Task<IActionResult> UnblockFoodAsync(
        Guid foodId,
        [FromServices] UnblockFoodHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(handler.HandleAsync(
            new UnblockFoodCommand(foodId), cancellationToken));

    [HttpPost("exercises/{exerciseId:guid}/block")]
    public Task<IActionResult> BlockExerciseAsync(
        Guid exerciseId,
        [FromBody] BlockContentRequest request,
        [FromServices] BlockExerciseHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(handler.HandleAsync(
            new BlockExerciseCommand(exerciseId, request.ReasonCode), cancellationToken));

    [HttpPost("exercises/{exerciseId:guid}/unblock")]
    public Task<IActionResult> UnblockExerciseAsync(
        Guid exerciseId,
        [FromServices] UnblockExerciseHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(handler.HandleAsync(
            new UnblockExerciseCommand(exerciseId), cancellationToken));

    private async Task<IActionResult> RespondAsync(Task<Result> operation)
    {
        var result = await operation;
        return result.IsSuccess ? NoContent() : ApiResultMapper.ToProblem(this, result.Error!);
    }
}
