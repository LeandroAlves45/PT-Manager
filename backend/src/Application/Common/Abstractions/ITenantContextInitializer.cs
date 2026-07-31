namespace Application.Common.Abstractions;

/// <summary>
/// Estabelece uma única vez a identidade efetiva do scope atual.
/// A separação impede que handlers de negócio possam redefinir o tenant.
/// </summary>
public interface ITenantContextInitializer
{
    void Establish(
        Guid? trainerId,
        Guid? userId,
        string? role,
        TenantOrigin origin,
        bool isAdministrative
    );
}
