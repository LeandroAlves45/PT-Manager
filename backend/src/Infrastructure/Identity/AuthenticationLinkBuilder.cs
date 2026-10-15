namespace Infrastructure.Identity;

/// <summary>
/// Constrói os links que os emails de autenticação apontam para o frontend. Partilhado
/// pelo adapter real e pelo de desenvolvimento, para o link registado em log ser o enviado.
/// </summary>
internal static class AuthenticationLinkBuilder
{
    internal const string ConfirmEmailPath = "confirmar-email";
    internal const string AcceptInvitePath = "aceitar-convite";
    internal const string PasswordResetPath = "repor-password";
    internal const string SignInPath = "entrar";
    internal const string ForgotPasswordPath = "recuperar-password";

    /// <summary>Junta o caminho à base do frontend, com o token em query string quando existe.</summary>
    internal static string Build(Uri frontendBaseUrl, string path, string? rawToken = null)
    {
        var builder = new UriBuilder(new Uri(frontendBaseUrl, path));
        if (rawToken is not null)
            builder.Query = $"token={Uri.EscapeDataString(rawToken)}";

        return builder.Uri.ToString();
    }
}
