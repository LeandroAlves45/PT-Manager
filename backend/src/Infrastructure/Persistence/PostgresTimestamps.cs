namespace Infrastructure.Persistence;

/// <summary>
/// Alinha instantes .NET (100 ns) com a precisão de <c>timestamp with time zone</c> do
/// PostgreSQL (microssegundos).
/// </summary>
/// <remarks>
/// Necessário nas escritas idempotentes: a primeira resposta devolve o valor em memória e a
/// repetição devolve o valor lido da DB. Sem truncar, o mesmo instante difere nos últimos
/// dígitos e o contrato "repetir devolve o mesmo" deixa de ser verdade.
/// </remarks>
internal static class PostgresTimestamps
{
    private const long TicksPerMicrosecond = TimeSpan.TicksPerMillisecond / 1000;

    /// <summary>Trunca o instante aos microssegundos, preservando o <see cref="DateTimeKind"/>.</summary>
    internal static DateTime Truncate(DateTime value) =>
        new(value.Ticks - value.Ticks % TicksPerMicrosecond, value.Kind);
}
