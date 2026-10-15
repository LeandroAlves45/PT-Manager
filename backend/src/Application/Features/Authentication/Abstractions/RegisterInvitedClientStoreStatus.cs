namespace Application.Features.Authentication.Abstractions;

/// <summary>Estados esperados do registo de um cliente a partir de um convite.</summary>
public enum RegisterInvitedClientStoreStatus
{
    /// <summary>Conta criada e sessão aberta.</summary>
    Registered,

    /// <summary>O token não corresponde a nenhum convite.</summary>
    TokenNotFound,

    /// <summary>O convite expirou.</summary>
    TokenExpired,

    /// <summary>O convite já foi usado.</summary>
    TokenAlreadyConsumed,

    /// <summary>Já existe uma conta com o email do convite.</summary>
    AccountAlreadyExists,

    /// <summary>A palavra-passe não cumpre a política da conta.</summary>
    PasswordRejected,

    /// <summary>A ficha foi removida, arquivada, já tem conta ou mudou de contacto.</summary>
    RelationshipConflict,

    /// <summary>Corrida de concorrência; o pedido pode ser repetido.</summary>
    ConcurrencyConflict
}
