namespace Api.Configuration;

/// <summary>Nomes estáveis das políticas aplicadas pelos endpoints sensíveis.</summary>
public static class ApiRateLimitPolicyNames
{
    public const string Login = "auth_login";
    public const string SignUp = "auth_signup";
    public const string Refresh = "auth_refresh";
    public const string Logout = "auth_logout";
    public const string CsrfBootstrap = "auth_csrf_bootstrap";
    public const string PasswordResetRequest = "auth_password_reset_request";
    public const string PasswordResetComplete = "auth_password_reset_complete";
    public const string EmailConfirmation = "auth_email_confirmation";
    public const string EmailConfirmationResend = "auth_email_confirmation_resend";
    public const string InviteClient = "auth_invite_client";
    public const string GoogleSignIn = "auth_google_sign_in";
    public const string GoogleLink = "auth_google_link";
    public const string ChangePassword = "auth_change_password";
    public const string Moderation = "admin_moderation";
}
