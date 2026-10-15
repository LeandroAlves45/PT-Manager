using Api.Authorization;
using Api.Contracts.Authentication;
using Api.Configuration;
using Application.Features.Authentication.GetAccount;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>Expõe os métodos de autenticação da conta autenticada.</summary>
/// <remarks>
/// Fora do <see cref="AuthController"/> de propósito: aquele exige Origin em todas as
/// ações por usarem o cookie de refresh, e esta leitura autentica-se só com o bearer.
/// </remarks>
[Route("api/v1/auth/account")]
[Authorize(ApiPolicyNames.Authenticated)]
[SensitiveResponse]
public sealed class AccountController : ApiControllerBase
{
    /// <summary>Devolve email, role e se a conta tem password local e Google ligado.</summary>
    [HttpGet]
    [ProducesResponseType<AccountResponse>(StatusCodes.Status200OK)]
    public Task<IActionResult> GetAsync(
        [FromServices] GetAccountHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(cancellationToken),
            AccountResponse.From);
}
