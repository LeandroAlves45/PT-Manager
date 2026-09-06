using Api.Authorization;
using Api.Configuration;
using Api.Contracts.Authentication;
using Api.Http;
using Api.Security;
using Application.Errors;
using Application.Features.Authentication.Google.IssueLinkChallenge;
using Application.Features.Authentication.Google.IssueSignInChallenge;
using Application.Features.Authentication.Google.Link;
using Application.Features.Authentication.Google.SignIn;
using Application.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Api.Controllers;

/// <summary>Expõe os quatro endpoints Google Sign-In e linking.</summary>
/// <remarks>
/// O controller não valida ID tokens nem recebe tenant no body; delega tudo
/// aos handlers e gere apenas cookies de challenge e refresh.
/// </remarks>
[ApiController]
[Route("api/v1/auth/google")]
[SensitiveResponse]
[RequireOrigin]
public sealed class GoogleAuthController : ApiControllerBase
{
    private readonly AuthCookieWriter _authCookies;
    private readonly GoogleChallengeCookieWriter _challengeCookies;

    public GoogleAuthController(
        AuthCookieWriter authCookies,
        GoogleChallengeCookieWriter challengeCookies)
    {
        _authCookies = authCookies ?? throw new ArgumentNullException(nameof(authCookies));
        _challengeCookies = challengeCookies ?? throw new ArgumentNullException(nameof(challengeCookies));
    }

    [HttpPost("challenge")]
    [EnableRateLimiting(ApiRateLimitPolicyNames.GoogleSignIn)]
    public async Task<IActionResult> ChallengeAsync(
        [FromServices] IssueGoogleSignInChallengeHandler handler,
        CancellationToken cancellationToken)
    {
        var challenge = await handler.HandleAsync(cancellationToken);
        _challengeCookies.Write(Response, challenge.Nonce, challenge.ExpiresAt);
        return Ok(GoogleChallengeResponse.From(challenge));
    }

    [HttpPost("sign-in")]
    [EnableRateLimiting(ApiRateLimitPolicyNames.GoogleSignIn)]
    public async Task<IActionResult> SignInAsync(
        [FromBody] GoogleSignInRequest request,
        [FromServices] GoogleSignInHandler handler,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await handler.HandleAsync(
                new GoogleSignInCommand(
                    request.IdToken,
                    GoogleChallengeCookieWriter.Read(Request) ?? string.Empty,
                    request.InvitationToken),
                cancellationToken);

            if (!result.IsSuccess)
                return Problem(result.Error!);

            if (result.Value.IsEmailConfirmationRequired)
                return Accepted(new GooglePendingResponse(
                    GooglePendingResponse.EmailConfirmationRequired));

            var session = result.Value.Session ?? throw new InvalidOperationException(
                "Successful Google sign-in has no session");

            _authCookies.Write(Response, session.RawRefreshToken, session.RefreshTokenExpiresAt);
            return Ok(SessionResponse.From(session));
        }
        finally
        {
            // Cada tentativa exige um challenge novo, mesmo quando a credencial falha.
            _challengeCookies.Delete(Response);
        }
    }

    [HttpPost("link/challenge")]
    [Authorize(ApiPolicyNames.Authenticated)]
    [EnableRateLimiting(ApiRateLimitPolicyNames.GoogleLink)]
    public async Task<IActionResult> LinkChallengeAsync(
        [FromServices] IssueGoogleLinkChallengeHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(cancellationToken);
        if (!result.IsSuccess)
            return Problem(result.Error!);

        _challengeCookies.Write(Response, result.Value.Nonce, result.Value.ExpiresAt);
        return Ok(GoogleChallengeResponse.From(result.Value));
    }

    [HttpPost("link")]
    [Authorize(ApiPolicyNames.Authenticated)]
    [EnableRateLimiting(ApiRateLimitPolicyNames.GoogleLink)]
    public async Task<IActionResult> LinkAsync(
        [FromBody] GoogleLinkRequest request,
        [FromServices] GoogleLinkHandler handler,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await handler.HandleAsync(
                new GoogleLinkCommand(
                    request.IdToken,
                    GoogleChallengeCookieWriter.Read(Request) ?? string.Empty,
                    request.CurrentPassword),
                cancellationToken);
            return result.IsSuccess ? NoContent() : Problem(result.Error!);
        }
        finally
        {
            _challengeCookies.Delete(Response);
        }
    }

    private IActionResult Problem(Error error) => ApiResultMapper.ToProblem(this, error);
}
