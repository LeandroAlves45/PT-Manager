namespace Application.Features.ClientPortal.Abstractions;

/// <summary>Desfecho da eliminação da conta pelo próprio cliente.</summary>
public enum MyAccountDeletionStatus
{
    /// <summary>Conta eliminada e dados pessoais anonimizados.</summary>
    Deleted,

    /// <summary>Não há ficha ativa ligada a esta conta no tenant (já eliminada ou arquivada).</summary>
    NotFound,

    /// <summary>A conta tem password e o pedido não a trouxe.</summary>
    PasswordRequired,

    /// <summary>Password errada ou conta bloqueada por tentativas falhadas.</summary>
    PasswordInvalid
}

/// <summary>Elimina a conta do cliente autenticado e anonimiza os seus dados.</summary>
public interface IMyAccountDeletionStore
{
    /// <summary>
    /// Numa só transação: confirma a password (com o mesmo lockout do login), arquiva a
    /// ficha com os mesmos efeitos do arquivo pelo personal trainer, apaga os dados que
    /// identificam o cliente, revoga as sessões e agenda a eliminação do avatar.
    /// </summary>
    Task<MyAccountDeletionStatus> DeleteAsync(
        Guid trainerId,
        Guid clientUserId,
        string? password,
        Guid correlationId,
        DateTime now,
        CancellationToken cancellationToken);
}
