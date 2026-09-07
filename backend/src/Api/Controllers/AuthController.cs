using Api.Authorization;
using Api.Configuration;
using Api.Contracts.Authentication;
using Api.Http;
using Api.Security;
using Application.Errors;
using Application.Features.Authentication;
using Application.Features.Authentication.AcceptClientInvite;
using Application.Features.Authentication.BootstrapCsrf;
using Application.Features.Authentication.ChangePassword;
using Application.Features.Authentication.ConfirmEmail;
using Application.Features.Authentication.Dtos;
using Application.Features.Authentication.InviteClient;
using Application.Features.Authentication.Login;
using Application.Features.Authentication.Logout;
using Application.Features.Authentication.RefreshSession;
using Application.Features.Authentication.RegisterTrainer;
using Application.Features.Authentication.RequestPasswordReset;
using Application.Features.Authentication.ResendEmailConfirmation;
using Application.Features.Authentication.ResetPassword;
using Application.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Api.Controllers;

/// <summary>Expõe os casos de uso de autenticação local.</summary>
/// <remarks>
/// SensitiveResponse na classe inteira pois toda a resposta transporta dados sensíveis,
/// como tokens identificadores ou só o fato de uma conta existir.
/// </remarks>
[ApiController]
[Route("api/v1/auth")]
[SensitiveResponse]
[RequireOrigin]
public sealed class AuthController : ControllerBase
{
    /// <summary>Nome do header que transporta o segredo anti-CSRF.</summary>
    public const string CsrfHeaderName = "X-CSRF-Token";

    private readonly AuthCookieWriter _cookies;
    private readonly ILogger<AuthController> _logger;

    /// <summary>Inicializa o controlador com o escritor de cookies e logger de segurança.</summary>
    public AuthController(AuthCookieWriter cookies, ILogger<AuthController> logger)
    {
        _cookies = cookies ?? throw new ArgumentNullException(nameof(cookies));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpPost("login")]
    [EnableRateLimiting(ApiRateLimitPolicyNames.Login)]
    public async Task<IActionResult> LoginAsync(
        [FromBody] LoginRequest request,
        [FromServices] LoginHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new LoginCommand(request.Email, request.Password), cancellationToken);
        LogResult(SecurityLogEvents.Login, "login", result);
        return CompleteSession(result);
    }

    [HttpPost("signup")]
    [EnableRateLimiting(ApiRateLimitPolicyNames.SignUp)]
    public async Task<IActionResult> SignUpAsync(
        [FromBody] SignUpRequest request,
        [FromServices] RegisterTrainerHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new RegisterTrainerCommand(request.Email, request.Password, request.FullName),
            cancellationToken);

        LogResult(SecurityLogEvents.SignUp, "signup", result);

        return result.IsSuccess
            ? Created(string.Empty, SignUpResponse.From(result.Value))
            : Problem(result.Error!);
    }

    [HttpPost("refresh")]
    [EnableRateLimiting(ApiRateLimitPolicyNames.Refresh)]
    public async Task<IActionResult> RefreshAsync(
        [FromServices] RefreshSessionHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new RefreshSessionCommand(ReadRefreshCookie(), ReadCsrfHeader()), cancellationToken);

        LogResult(SecurityLogEvents.RefreshRotation, "refresh", result);
        LogCsrfRejection("refresh", result);
        return CompleteSession(result);
    }

    [HttpPost("logout")]
    [EnableRateLimiting(ApiRateLimitPolicyNames.Logout)]
    public async Task<IActionResult> LogoutAsync(
        [FromServices] LogoutHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new LogoutCommand(ReadRefreshCookie(), ReadCsrfHeader()),
            cancellationToken);

        LogResult(SecurityLogEvents.Logout, "logout", result);
        LogCsrfRejection("logout", result);
        if (!result.IsSuccess)
            return Problem(result.Error!);

        // Cookie só é eliminado depois de a revogação ter sucedido. Apagá-lo
        // primeiro deixaria o cliente sem forma de repetir o logout se a escrita
        // falhasse, e a sessão sobreviveria no servidor sem dono.
        _cookies.Delete(Response);
        return NoContent();
    }

    [HttpPost("csrf")]
    [EnableRateLimiting(ApiRateLimitPolicyNames.CsrfBootstrap)]
    public async Task<IActionResult> BootstrapCsrfAsync(
        [FromServices] BootstrapCsrfHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new BootstrapCsrfCommand(ReadRefreshCookie()),
            cancellationToken);

        return result.IsSuccess
            ? Ok(CsrfResponse.From(result.Value))
            : Problem(result.Error!);
    }

    [HttpPost("confirm-email")]
    [EnableRateLimiting(ApiRateLimitPolicyNames.EmailConfirmation)]
    public async Task<IActionResult> ConfirmEmailAsync(
        [FromBody] ConfirmEmailRequest request,
        [FromServices] ConfirmEmailHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new ConfirmEmailCommand(request.Token),
            cancellationToken);
        LogResult(SecurityLogEvents.EmailConfirmation, "confirm_email", result);
        return Respond(result);
    }

    [HttpPost("resend-confirmation")]
    [Authorize(ApiPolicyNames.Authenticated)]
    [EnableRateLimiting(ApiRateLimitPolicyNames.EmailConfirmationResend)]
    public async Task<IActionResult> ResendConfirmationAsync(
        [FromServices] ResendEmailConfirmationHandler handler,
        CancellationToken cancellationToken) =>
        Respond(await handler.HandleAsync(cancellationToken));

    [HttpPost("password-reset/request")]
    [EnableRateLimiting(ApiRateLimitPolicyNames.PasswordResetRequest)]
    public async Task<IActionResult> RequestPasswordResetAsync(
        [FromBody] PasswordResetRequest request,
        [FromServices] RequestPasswordResetHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new RequestPasswordResetCommand(request.Email),
            cancellationToken);
        LogResult(SecurityLogEvents.PasswordReset, "request_password_reset", result);
        return Respond(result);
    }

    [HttpPost("password-reset/complete")]
    [EnableRateLimiting(ApiRateLimitPolicyNames.PasswordResetComplete)]
    public async Task<IActionResult> CompletePasswordResetAsync(
        [FromBody] PasswordResetCompletionRequest request,
        [FromServices] ResetPasswordHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new ResetPasswordCommand(
                request.Token,
                request.NewPassword,
                request.ConfirmNewPassword),
            cancellationToken);
        LogResult(SecurityLogEvents.PasswordReset, "complete_password_reset", result);
        return Respond(result);
    }

    [HttpPost("change-password")]
    [Authorize(ApiPolicyNames.Authenticated)]
    [EnableRateLimiting(ApiRateLimitPolicyNames.ChangePassword)]
    public async Task<IActionResult> ChangePasswordAsync(
        [FromBody] ChangePasswordRequest request,
        [FromServices] ChangePasswordHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new ChangePasswordCommand(
                request.CurrentPassword,
                request.NewPassword,
                request.ConfirmNewPassword),
            cancellationToken);
        LogResult(SecurityLogEvents.PasswordChange, "change_password", result);
        return Respond(result);
    }

    [HttpPost("accept-invite")]
    [Authorize(ApiPolicyNames.Client)]
    [EnableRateLimiting(ApiRateLimitPolicyNames.InviteClient)]
    public async Task<IActionResult> AcceptInviteAsync(
        [FromBody] AcceptInvitationRequest request,
        [FromServices] AcceptClientInviteHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new AcceptClientInviteCommand(request.Token, request.TransferApproved),
            cancellationToken);
        LogResult(SecurityLogEvents.SignUp, "accept_invite", result);
        return CompleteSession(result);
    }

    [HttpPost("invite-client")]
    [Authorize(ApiPolicyNames.Trainer)]
    [EnableRateLimiting(ApiRateLimitPolicyNames.InviteClient)]
    public async Task<IActionResult> InviteClientAsync(
        [FromBody] InviteClientRequest request,
        [FromServices] InviteClientHandler handler,
        CancellationToken cancellationToken) =>
        Respond(await handler.HandleAsync(
            new InviteClientCommand(request.ClientId, request.Email),
            cancellationToken));

    /// <summary>Emite o cookie e devolve a sessão sem o refresh token no corpo.</summary>
    private IActionResult CompleteSession(Result<AuthenticationSessionDto> result)
    {
        if (!result.IsSuccess)
            return Problem(result.Error!);

        var session = result.Value;
        _cookies.Write(Response, session.RawRefreshToken, session.RefreshTokenExpiresAt);
        return Ok(SessionResponse.From(session));
    }

    /// <summary>Regista sucesso ou recusa de uma operação de segurança.</summary>
    private void LogResult(EventId eventId, string operation, Result result)
    {
        if (result.IsSuccess)
        {
            _logger.LogInformation(eventId,
                "Security operation {SecurityOperation} completed with outcome {SecurityOutcome}.",
                operation, "succeeded");
            return;
        }

        _logger.LogWarning(eventId,
            "Security operation {SecurityOperation} completed with outcome {SecurityOutcome} and error {ErrorCode}.",
            operation, "rejected", result.Error!.Code);
    }

    /// <summary>Regista recusas explícitas por token CSRF inválido.</summary>
    private void LogCsrfRejection(string operation, Result result)
    {
        if (result.Error?.Code != AuthenticationErrors.CsrfTokenInvalid.Code)
            return;

        _logger.LogWarning(SecurityLogEvents.CsrfRejection,
            "Security operation {SecurityOperation} was rejected by CSRF validation.", operation);
    }

    /// <summary>Devolve 204 quando a operação não produz corpo de resposta.</summary>
    private IActionResult Respond(Result result) =>
        result.IsSuccess ? NoContent() : Problem(result.Error!);

    /// <summary>Lê o cookie de refresh, devolvendo string vazia quando ausente.</summary>
    private string ReadRefreshCookie() => AuthCookieWriter.Read(Request) ?? string.Empty;

    /// <summary>Lê o header CSRF, devolvendo string vazia quando ausente ou ambíguo.</summary>
    private string ReadCsrfHeader() =>
        Request.Headers.TryGetValue(CsrfHeaderName, out var values) && values.Count == 1
            ? values[0] ?? string.Empty
            : string.Empty;

    /// <summary>Converte um erro da Application em Problem Details.</summary>
    private IActionResult Problem(Error error) => ApiResultMapper.ToProblem(this, error);
}
