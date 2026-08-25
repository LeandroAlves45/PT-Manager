namespace Application.Features.Authentication.ResetPassword;

/// <summary>Token e nova credencial apresentados para concluir o reset.</summary>
public sealed record ResetPasswordCommand(
    string Token,
    string NewPassword,
    string ConfirmNewPassword
);
