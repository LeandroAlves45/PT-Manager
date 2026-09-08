using Api.Configuration;
using Api.Middlewares;
using Application.Features.Jobs.Dispatching;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Api.Controllers;

/// <summary>Recebe ativações assinadas do scheduler externo.</summary>
[ApiController]
[Route("api/internal/jobs")]
[AllowAnonymous]
public sealed class InternalJobsController : ControllerBase
{
    private const string SignatureHeaderName = "Upstash-Signature";
    private const int AbsoluteMaximumBodySize = 64 * 1024;

    private readonly InternalJobDispatchHttpOptions _options;
    private readonly IInternalDispatchRequestAuthenticator _authenticator;
    private readonly IJobDispatchActivation _activation;
    private readonly IProblemDetailsService _problemDetailsService;

    public InternalJobsController(
        IOptions<InternalJobDispatchHttpOptions> options,
        IInternalDispatchRequestAuthenticator authenticator,
        IJobDispatchActivation activation,
        IProblemDetailsService problemDetailsService)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
        _authenticator = authenticator ?? throw new ArgumentNullException(nameof(authenticator));
        _activation = activation ?? throw new ArgumentNullException(nameof(activation));
        _problemDetailsService = problemDetailsService ??
            throw new ArgumentNullException(nameof(problemDetailsService));
    }

    /// <summary>Ativa uma passagem limitada pelas filas Postgres.</summary>
    [HttpPost("dispatch")]
    [Tags("internal-jobs")]
    [EndpointName("DispatchInternalJobs")]
    [Consumes("application/json")]
    [EnableRateLimiting(InternalJobDispatchPolicyNames.Dispatch)]
    [RequestSizeLimit(AbsoluteMaximumBodySize)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> DispatchAsync(CancellationToken cancellationToken)
    {
        if (!TryReadSingleHeader(Request.Headers[SignatureHeaderName], out var signature))
            return await WriteProblemAsync(
                StatusCodes.Status401Unauthorized,
                "Unauthorized",
                "The dispatch request signature is invalid.");

        var body = await ReadRawBodyAsync(cancellationToken);
        if (body is null)
            return await WriteProblemAsync(
                StatusCodes.Status413PayloadTooLarge,
                "Payload too large",
                "The dispatch request body exceeds the allowed limit.");

        var authentication = await _authenticator.AuthenticateAsync(
            signature,
            body,
            cancellationToken);

        if (authentication.Status == InternalDispatchAuthenticationStatus.Invalid)
            return await WriteProblemAsync(
                StatusCodes.Status401Unauthorized,
                "Unauthorized",
                "The dispatch request signature is invalid.");

        if (authentication.Status == InternalDispatchAuthenticationStatus.Unavailable)
            return await WriteProblemAsync(
                StatusCodes.Status503ServiceUnavailable,
                "Service unavailable",
                "The dispatch request cannot be accepted at this time.");

        if (authentication.Status == InternalDispatchAuthenticationStatus.Replay)
            return NoContent();

        await _activation.ActivateAsync(cancellationToken);
        return NoContent();
    }

    private async Task<byte[]?> ReadRawBodyAsync(CancellationToken cancellationToken)
    {
        if (Request.ContentLength > _options.MaximumBodySize)
            return null;

        var buffer = new byte[_options.MaximumBodySize + 1];
        var totalRead = 0;

        while (totalRead < buffer.Length)
        {
            var read = await Request.Body.ReadAsync(
                buffer.AsMemory(totalRead, buffer.Length - totalRead),
                cancellationToken);
            if (read == 0)
                break;

            totalRead += read;
        }

        if (totalRead > _options.MaximumBodySize)
            return null;

        return buffer.AsSpan(0, totalRead).ToArray();
    }

    private static bool TryReadSingleHeader(StringValues values, out string value)
    {
        value = string.Empty;
        if (values.Count != 1 || string.IsNullOrWhiteSpace(values[0]))
            return false;

        value = values[0]!;
        return true;
    }

    private async Task<IActionResult> WriteProblemAsync(
        int statusCode,
        string title,
        string detail)
    {
        await ProblemDetailsResponseWriter.WriteAsync(
            HttpContext,
            _problemDetailsService,
            statusCode,
            title,
            detail);

        return new EmptyResult();
    }
}
