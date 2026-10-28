using Api.Authorization;
using Api.Configuration;
using Api.Contracts.Authentication;
using Api.Contracts.Common;
using Api.Http;
using Api.Security;
using Application.Errors;
using Application.Features.Authentication.AcceptClientInvite;
using Application.Features.Authentication.Dtos;
using Application.Features.Authentication.Login;
using Application.Features.Authentication.Logout;
using Application.Features.Authentication.RefreshSession;
using Application.Features.Authentication.RegisterInvitedClient;
using Application.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Api.Controllers;

/// <summary>Sessão da app nativa (Android e iOS), com o refresh token no corpo.</summary>
/// <remarks>
/// <para>
/// A app corre numa WebView com origem própria (<c>capacitor://localhost</c> ou
/// <c>https://localhost</c>), que é outro site em relação à API: o cookie de refresh do
/// <see cref="AuthController"/> não seria enviado. Aqui o refresh token vai e volta no corpo
/// e a app guarda-o no armazenamento seguro do sistema.
/// </para>
/// <para>
/// Nenhuma ação lê ou escreve cookies, por isso não há credencial ambiente que um site
/// malicioso possa usar: é por isso que não leva <see cref="RequireOriginAttribute"/>, tal
/// como o <see cref="AccountController"/>. Mesmo assim, rodar e terminar a sessão exigem o
/// segredo anti-CSRF da sessão: são os mesmos handlers e o mesmo store da sessão web, com
/// rotação, deteção de reuso e revogação da família iguais.
/// </para>
/// <para>
/// Rotas fora de <c>/api/v1/auth</c>: o browser nunca anexa o cookie de refresh (o path do
/// cookie) a estes pedidos. Os limites por IP são os mesmos da sessão web.
/// </para>
/// </remarks>
[ApiController]
[Route("api/v1/native/auth")]
[SensitiveResponse]
[ProducesResponseType<ApiProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ApiProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ApiProblemDetails>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ApiProblemDetails>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ApiProblemDetails>(StatusCodes.Status409Conflict)]
[ProducesResponseType<ApiProblemDetails>(StatusCodes.Status429TooManyRequests)]
[ProducesResponseType<ApiProblemDetails>(StatusCodes.Status500InternalServerError)]
[ProducesResponseType<ApiProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
public sealed class NativeAuthController : ControllerBase
{
    private readonly ILogger<NativeAuthController> _logger;

    /// <summary>Inicializa o controlador com o logger de segurança.</summary>
    public NativeAuthController(ILogger<NativeAuthController> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Inicia sessão com email e password e devolve os tokens no corpo.</summary>
    [HttpPost("login")]
    [ProducesResponseType<NativeSessionResponse>(StatusCodes.Status200OK)]
    [EnableRateLimiting(ApiRateLimitPolicyNames.Login)]
    public async Task<IActionResult> LoginAsync(
        [FromBody] LoginRequest request,
        [FromServices] LoginHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new LoginCommand(request.Email, request.Password), cancellationToken);
        AuthenticationSecurityLog.Result(_logger, SecurityLogEvents.Login, "native_login", result);
        return CompleteSession(result);
    }

    /// <summary>
    /// Roda a sessão: o refresh token enviado deixa de valer e é devolvido um novo. Reenviar
    /// um token já rodado revoga a família inteira (provável roubo).
    /// </summary>
    [HttpPost("refresh")]
    [ProducesResponseType<NativeSessionResponse>(StatusCodes.Status200OK)]
    [EnableRateLimiting(ApiRateLimitPolicyNames.Refresh)]
    public async Task<IActionResult> RefreshAsync(
        [FromBody] NativeSessionTokensRequest request,
        [FromServices] RefreshSessionHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new RefreshSessionCommand(request.RefreshToken ?? string.Empty, request.CsrfToken ?? string.Empty),
            cancellationToken);

        AuthenticationSecurityLog.Result(_logger, SecurityLogEvents.RefreshRotation, "native_refresh", result);
        AuthenticationSecurityLog.CsrfRejection(_logger, "native_refresh", result);
        return CompleteSession(result);
    }

    /// <summary>Termina a sessão no servidor. Um token desconhecido também devolve 204.</summary>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [EnableRateLimiting(ApiRateLimitPolicyNames.Logout)]
    public async Task<IActionResult> LogoutAsync(
        [FromBody] NativeSessionTokensRequest request,
        [FromServices] LogoutHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new LogoutCommand(request.RefreshToken ?? string.Empty, request.CsrfToken ?? string.Empty),
            cancellationToken);

        AuthenticationSecurityLog.Result(_logger, SecurityLogEvents.Logout, "native_logout", result);
        AuthenticationSecurityLog.CsrfRejection(_logger, "native_logout", result);
        return result.IsSuccess ? NoContent() : Problem(result.Error!);
    }

    /// <summary>Um cliente com sessão aceita o convite de outro personal trainer.</summary>
    [HttpPost("accept-invite")]
    [ProducesResponseType<NativeSessionResponse>(StatusCodes.Status200OK)]
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
        AuthenticationSecurityLog.Result(_logger, SecurityLogEvents.SignUp, "native_accept_invite", result);
        return CompleteSession(result);
    }

    /// <summary>Cria a conta de um cliente convidado e abre a sessão.</summary>
    [HttpPost("accept-invite/register")]
    [ProducesResponseType<NativeSessionResponse>(StatusCodes.Status200OK)]
    [EnableRateLimiting(ApiRateLimitPolicyNames.InviteRegister)]
    public async Task<IActionResult> RegisterInvitedClientAsync(
        [FromBody] InvitedClientRegistrationRequest request,
        [FromServices] RegisterInvitedClientHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new RegisterInvitedClientCommand(
                request.Token,
                request.Password,
                request.ConfirmPassword),
            cancellationToken);
        AuthenticationSecurityLog.Result(
            _logger, SecurityLogEvents.SignUp, "native_register_invited_client", result);
        return CompleteSession(result);
    }

    /// <summary>Devolve a sessão com o refresh token no corpo, sem escrever cookies.</summary>
    private IActionResult CompleteSession(Result<AuthenticationSessionDto> result) =>
        result.IsSuccess
            ? Ok(NativeSessionResponse.From(result.Value))
            : Problem(result.Error!);

    /// <summary>Converte um erro da Application em Problem Details.</summary>
    private IActionResult Problem(Error error) => ApiResultMapper.ToProblem(this, error);
}
