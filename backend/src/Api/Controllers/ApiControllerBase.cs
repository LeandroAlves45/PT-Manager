using Api.Http;
using Application.Errors;
using Application.Results;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>
/// Base dos controllers de negócio. Concentra a tradução de <see cref="Result"/>
/// e <see cref="Result{TValue}"/> em respostas HTTP, para que nenhum controller
/// volte a decidir códigos de estado por si.
/// </summary>
[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>Converte um resultado sem valor em 204 ou Problem Details.</summary>
    protected async Task<IActionResult> RespondAsync(Task<Result> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        var result = await operation;
        return result.IsSuccess
            ? NoContent()
            : ApiResultMapper.ToProblem(this, result.Error!);
    }

    /// <summary>Converte um resultado com valor em 200 ou Problem Details.</summary>
    protected async Task<IActionResult> RespondAsync<TValue, TResponse>(
        Task<Result<TValue>> operation,
        Func<TValue, TResponse> projection)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(projection);

        var result = await operation;
        return result.IsSuccess
            ? Ok(projection(result.Value))
            : ApiResultMapper.ToProblem(this, result.Error!);
    }

    /// <summary>Converte um resultado com valor em 201 ou Problem Details.</summary>
    protected async Task<IActionResult> RespondCreatedAsync<TValue, TResponse>(
        Task<Result<TValue>> operation,
        Func<TValue, TResponse> projection,
        Func<TValue, string> location)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(projection);
        ArgumentNullException.ThrowIfNull(location);

        var result = await operation;
        return result.IsSuccess
            ? Created(location(result.Value), projection(result.Value))
            : ApiResultMapper.ToProblem(this, result.Error!);
    }

    /// <summary>
    /// Converte um resultado cujo o valor pode ser nulo em 200, 404 ou Problem Details.
    /// </summary>
    protected async Task<IActionResult> RespondOptionalAsync<TValue, TResponse>(
        Task<Result<TValue?>> operation,
        Func<TValue, TResponse> projection)
        where TValue : class
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(projection);

        var result = await operation;
        if (!result.IsSuccess)
            return ApiResultMapper.ToProblem(this, result.Error!);

        return result.Value is null
            ? ApiResultMapper.ToProblem(this, NotFoundError())
            : Ok(projection(result.Value));
    }

    private static Error NotFoundError() => Error.Create(
        "resource_not_found",
        ErrorCategory.NotFound,
        "The resource was not found."
    );
}
