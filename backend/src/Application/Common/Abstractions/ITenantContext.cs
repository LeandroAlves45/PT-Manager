namespace Application.Common.Abstractions;

/// <summary>
/// Contexto do efetivo da operação em curso. A implementação concreta é
/// composta pela API/Infrastructure a partir de claims validadas ou do contexto
/// persistido de jobs e webhooks.
/// </summary>
public interface ITenantContext
{
    /// <summary>
    /// Trainer efetivo da operação — a chave usada pelos Global Query Filters.
    /// Para um utilizador com role "client", é o personal trainer dono desse cliente.
    /// Null apenas quando o caso de uso não opera sobre dados tenant-owned.
    /// Null nunca concede acesso global e não desativa filtros automaticamente.
    /// </summary>
    Guid? TrainerId { get; }

    /// <summary>Utilizador autenticado que originou a operação. Null em jobs de sistema.</summary>
    Guid? UserId { get; }

    /// <summary>Role do utilizador: "trainer", "client" ou "superuser". Null em jobs de sistema.</summary>
    string? Role { get; }

    /// <summary>Origem do contexto: de onde veio a edentidade desta operação.</summary>
    TenantOrigin Origin { get; }

    /// <summary>
    /// True apenas depois de uma política administrativa dedicada ter autorizado
    /// o caso de uso. Esta flag não desativa Global Query Filters por si só.
    /// Bypass exige handler dedicado, autorização, auditoria e teste cross-tenant.
    /// </summary>
    bool IsAdministrative { get; }
}

/// <summary>Origem da identidade da operação em curso.</summary>
public enum TenantOrigin
{
    /// <summary>Pedido HTTP autenticado com JWT.</summary>
    Http,
    /// <summary>Job do dispatcher (QStash) -> identidade vem da linha do job.</summary>
    Job,
    /// <summary>Webhook externo assinado (ex: Stripe) -> sem utilizador.</summary>
    Webhook,
    /// <summary>Processo de sistema (migrations, seeds, manutenções).</summary>
    System
}
