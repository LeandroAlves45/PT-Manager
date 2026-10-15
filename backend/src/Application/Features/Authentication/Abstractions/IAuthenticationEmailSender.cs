namespace Application.Features.Authentication.Abstractions;

/// <summary>Entrega links Auth diretamente depois do commit local.</summary>
public interface IAuthenticationEmailSender
{
    Task<AuthenticationEmailDeliveryOutcome> SendEmailConfirmationAsync(
        IssuedAuthenticationSecret secret,
        CancellationToken cancellationToken);

    Task<AuthenticationEmailDeliveryOutcome> SendClientInvitationAsync(
        IssuedAuthenticationSecret secret,
        CancellationToken cancellationToken);

    Task<AuthenticationEmailDeliveryOutcome> SendPasswordResetAsync(
        IssuedAuthenticationSecret secret,
        CancellationToken cancellationToken);

    /// <summary>
    /// Avisa o dono de um email que já tem conta e alguém tentou registá-lo outra vez.
    /// Não transporta segredo: aponta apenas para entrar e para recuperar a password.
    /// </summary>
    Task<AuthenticationEmailDeliveryOutcome> SendAccountAlreadyExistsAsync(
        string recipientEmail,
        CancellationToken cancellationToken);
}
