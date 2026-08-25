namespace Application.Features.Authentication;

/// <summary>Configuração validada dos fluxos locais de autenticação.</summary>
public sealed class AuthenticationPolicy
{

    public int TrialDays { get; }
    public TimeSpan EmailConfirmationLifetime { get; }
    public TimeSpan ClientInviteLifetime { get; }
    public TimeSpan PasswordResetLifetime { get; }
    public TimeSpan RefreshSessionLifetime { get; }

    /// <summary>Cria a política com os defaults aprovados para o MVP.</summary>
    public AuthenticationPolicy(
        int trialDays = 15,
        TimeSpan? emailConfirmationLifetime = null,
        TimeSpan? clientInviteLifetime = null,
        TimeSpan? passwordResetLifetime = null,
        TimeSpan? refreshSessionLifetime = null)
    {
        TrialDays = trialDays;
        EmailConfirmationLifetime = emailConfirmationLifetime ?? TimeSpan.FromHours(24);
        ClientInviteLifetime = clientInviteLifetime ?? TimeSpan.FromDays(7);
        PasswordResetLifetime = passwordResetLifetime ?? TimeSpan.FromHours(1);
        RefreshSessionLifetime = refreshSessionLifetime ?? TimeSpan.FromDays(30);

        if (TrialDays <= 0)
            throw new ArgumentOutOfRangeException(nameof(trialDays));
        if (EmailConfirmationLifetime <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(emailConfirmationLifetime));
        if (ClientInviteLifetime <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(clientInviteLifetime));
        if (PasswordResetLifetime <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(passwordResetLifetime));
        if (RefreshSessionLifetime <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(refreshSessionLifetime));
    }
}

