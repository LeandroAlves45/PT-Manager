using Application.Features.Authentication;
using Application.Results;

namespace Api.Security;

/// <summary>
/// Registo dos resultados das operações de autenticação, partilhado pelos controladores da
/// sessão web (cookie) e da sessão nativa (token no corpo), para os dois caminhos ficarem
/// com os mesmos eventos e campos nos logs.
/// </summary>
internal static class AuthenticationSecurityLog
{
    /// <summary>Regista sucesso ou recusa de uma operação de segurança.</summary>
    public static void Result(ILogger logger, EventId eventId, string operation, Result result)
    {
        if (result.IsSuccess)
        {
            logger.LogInformation(eventId,
                "Security operation {SecurityOperation} completed with outcome {SecurityOutcome}.",
                operation, "succeeded");
            return;
        }

        logger.LogWarning(eventId,
            "Security operation {SecurityOperation} completed with outcome {SecurityOutcome} and error {ErrorCode}.",
            operation, "rejected", result.Error!.Code);
    }

    /// <summary>Regista recusas explícitas por token CSRF inválido.</summary>
    public static void CsrfRejection(ILogger logger, string operation, Result result)
    {
        if (result.Error?.Code != AuthenticationErrors.CsrfTokenInvalid.Code)
            return;

        logger.LogWarning(SecurityLogEvents.CsrfRejection,
            "Security operation {SecurityOperation} was rejected by CSRF validation.", operation);
    }
}
