using Api.Authorization;
using Api.Configuration;
using Api.Contracts.Authentication;
using Api.Http;
using Api.Security;
using Application.Errors;
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

    public AuthController(AuthCookieWriter cookies) =>
        _cookies = cookies ?? throw new ArgumentNullException(nameof(cookies));

    [HttpPost("login")]
    [EnableRateLimiting(ApiRateLimitPolicyNames.Login)]
    public Task<IActionResult> LoginAsync(
        [FromBody] LoginRequest request,
        [FromServices] LoginHandler handler,
        CancellationToken cancellationToken) =>
        RunSessionAsync(
            handler.HandleAsync(
                new LoginCommand(request.Email, request.Password),
                cancellationToken));

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

        return result.IsSuccess
            ? Created(string.Empty, SignUpResponse.From(result.Value))
            : Problem(result.Error!);
    }

    [HttpPost("refresh")]
    [EnableRateLimiting(ApiRateLimitPolicyNames.Refresh)]
    public Task<IActionResult> RefreshAsync(
        [FromServices] RefreshSessionHandler handler,
        CancellationToken cancellationToken) =>
        RunSessionAsync(
            handler.HandleAsync(
                new RefreshSessionCommand(ReadRefreshCookie(), ReadCsrfHeader()),
                cancellationToken));

    [HttpPost("logout")]
    [EnableRateLimiting(ApiRateLimitPolicyNames.Logout)]
    public async Task<IActionResult> LogoutAsync(
        [FromServices] LogoutHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new LogoutCommand(ReadRefreshCookie(), ReadCsrfHeader()),
            cancellationToken);

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
        CancellationToken cancellationToken) =>
        Respond(await handler.HandleAsync(
            new ConfirmEmailCommand(request.Token),
            cancellationToken));

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
        CancellationToken cancellationToken) =>
        Respond(await handler.HandleAsync(
            new RequestPasswordResetCommand(request.Email),
            cancellationToken));

    [HttpPost("password-reset/complete")]
    [EnableRateLimiting(ApiRateLimitPolicyNames.PasswordResetComplete)]
    public async Task<IActionResult> CompletePasswordResetAsync(
        [FromBody] PasswordResetCompletionRequest request,
        [FromServices] ResetPasswordHandler handler,
        CancellationToken cancellationToken) =>
        Respond(await handler.HandleAsync(
            new ResetPasswordCommand(
                request.Token,
                request.NewPassword,
                request.ConfirmNewPassword),
            cancellationToken));

    [HttpPost("change-password")]
    [Authorize(ApiPolicyNames.Authenticated)]
    [EnableRateLimiting(ApiRateLimitPolicyNames.ChangePassword)]
    public async Task<IActionResult> ChangePasswordAsync(
        [FromBody] ChangePasswordRequest request,
        [FromServices] ChangePasswordHandler handler,
        CancellationToken cancellationToken) =>
        Respond(await handler.HandleAsync(
            new ChangePasswordCommand(
                request.CurrentPassword,
                request.NewPassword,
                request.ConfirmNewPassword),
            cancellationToken));

    [HttpPost("accept-invite")]
    [Authorize(ApiPolicyNames.Client)]
    [EnableRateLimiting(ApiRateLimitPolicyNames.InviteClient)]
    public Task<IActionResult> AcceptInviteAsync(
        [FromBody] AcceptInvitationRequest request,
        [FromServices] AcceptClientInviteHandler handler,
        CancellationToken cancellationToken) =>
        RunSessionAsync(
            handler.HandleAsync(
                new AcceptClientInviteCommand(request.Token, request.TransferApproved),
                cancellationToken));

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
    private async Task<IActionResult> RunSessionAsync(
        Task<Result<AuthenticationSessionDto>> operation)
    {
        var result = await operation;
        if (!result.IsSuccess)
            return Problem(result.Error!);

        var session = result.Value;
        _cookies.Write(Response, session.RawRefreshToken, session.RefreshTokenExpiresAt);
        return Ok(SessionResponse.From(session));
    }

    private IActionResult Respond(Result result) =>
        result.IsSuccess ? NoContent() : Problem(result.Error!);

    /// <summary>Lê o cookie de refresh, devolvendo string vazia quando ausente.</summary>
    private string ReadRefreshCookie() => AuthCookieWriter.Read(Request) ?? string.Empty;

    private string ReadCsrfHeader() =>
        Request.Headers.TryGetValue(CsrfHeaderName, out var values) && values.Count == 1
            ? values[0] ?? string.Empty
            : string.Empty;

    /// <summary>Converte um erro da Application em Problem Details.</summary>
    private IActionResult Problem(Error error) => ApiResultMapper.ToProblem(this, error);
}
