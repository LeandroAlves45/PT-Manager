using Application.Errors;

namespace Application.Features.Administration.Overview;

/// <summary>Erros estáveis da visão geral administrativa.</summary>
public static class AdminOverviewErrors
{
    /// <summary>Só um superuser em contexto administrativo pode ler a visão geral.</summary>
    public static readonly Error AdministratorOnly = Error.Create(
        "admin_overview_administrator_only",
        ErrorCategory.Forbidden,
        "Only an active superuser in administrative context can read the platform overview.");
}
