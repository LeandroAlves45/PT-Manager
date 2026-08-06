namespace Application.Errors;

/// <summary>
/// Catálogo de erros partilhados entre features da Application.
/// Erros específicos de uma feature ficam junto ao handler, não aqui.
/// </summary>
public static class CommonErrors
{
    /// <summary>Operação exige tenant e nenhum foi resolvido.</summary>
    public static readonly Error TenantRequired = Error.Create(
        code: "tenant_required",
        category: ErrorCategory.Forbidden,
        description: "Tenant is required for this operation."
    );

    /// <summary>Requisição não tem identidade autenticada válida.</summary>
    public static readonly Error UnauthenticatedUser = Error.Create(
        code: "unauthenticated_user",
        category: ErrorCategory.Unauthorized,
        description: "User is not authenticated."
    );
}
