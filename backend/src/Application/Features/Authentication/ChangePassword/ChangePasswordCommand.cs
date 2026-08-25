namespace Application.Features.Authentication.ChangePassword;

/// <summary>Credenciais necessárias para mudar a password atual.</summary>
public sealed record ChangePasswordCommand(
    string CurrentPassword,
    string NewPassword,
    string ConfirmNewPassword
);
