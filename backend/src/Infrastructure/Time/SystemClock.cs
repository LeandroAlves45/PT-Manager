using Application.Common.Abstractions;

namespace Infrastructure.Time;

/// <summary>
/// Relógio de produção: devolve a hora real do sistema em UTC.
/// </summary>
internal sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
