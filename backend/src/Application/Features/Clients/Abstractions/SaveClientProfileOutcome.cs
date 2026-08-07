namespace Application.Features.Clients.Abstractions;

/// <summary>Outcomes da persistência de perfis de clientes.</summary>
public enum SaveClientProfileOutcome
{
    /// <summary>Perfil confirmado.</summary>
    Updated,
    /// <summary>Email colide com outro cliente do tenant.</summary>
    DuplicateEmail,
    /// <summary>Telefone colide com outro cliente do tenant.</summary>
    DuplicatePhone
}
