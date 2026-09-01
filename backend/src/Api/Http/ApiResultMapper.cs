using Application.Errors;
using Microsoft.AspNetCore.Mvc;

namespace Api.Http;

/// <summary>Converte erros da Application no contrato Problem Details comum na API.</summary>
internal static class ApiResultMapper
{
    internal static IActionResult ToProblem(ControllerBase controller, Error error)
    {
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(error);

        var status = error.Category switch
        {
            ErrorCategory.Validation => StatusCodes.Status400BadRequest,
            ErrorCategory.NotFound => StatusCodes.Status404NotFound,
            ErrorCategory.Conflict => StatusCodes.Status409Conflict,
            ErrorCategory.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorCategory.Forbidden => StatusCodes.Status403Forbidden,
            ErrorCategory.PaymentRequired => StatusCodes.Status402PaymentRequired,
            ErrorCategory.ExternalDependency => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status500InternalServerError
        };

        var problem = new ProblemDetails
        {
            Status = status,
            Title = error.Code,
            Detail = error.Description,
            Instance = controller.Request.Path
        };
        problem.Extensions["correlation_id"] = controller.HttpContext.TraceIdentifier;

        if (error.Category == ErrorCategory.Validation)
        {
            problem.Extensions["errors"] = error.ValidationErrors.Select(validation => new
            {
                field = validation.Field,
                code = validation.Code,
                message = validation.Message
            }).ToArray();
        }

        if (status == StatusCodes.Status401Unauthorized)
            controller.Response.Headers.WWWAuthenticate = "Bearer";

        return controller.StatusCode(status, problem);
    }
}
