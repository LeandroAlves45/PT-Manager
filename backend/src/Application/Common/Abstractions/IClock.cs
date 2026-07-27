namespace Application.Common.Abstractions;

/// <summary>
/// Fonte do instante atual em UTC. Injetada nos handlers para que o tempo
/// seja determinístico em testes (expiração de tokens, leases, agendamentos, etc).
/// </summary>
public interface IClock
{
    /// <summary>
    /// Instante atual em UTC.
    /// </summary>
    DateTime UtcNow { get; }
}
