using Application.Common.Abstractions;

namespace Infrastructure.IntegrationTests.Support;

/// <summary>
/// Suporte para os testes de integração que precisam de um relógio controlável.
/// </summary>
internal sealed class TestClock : IClock
{
    public TestClock(DateTime utcNow)
    {
        Set(utcNow);
    }

    public DateTime UtcNow { get; private set; }

    public void Advance(TimeSpan duration) => Set(UtcNow.Add(duration));

    public void Set(DateTime utcNow)
    {
        if (utcNow.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Test clock requires UTC", nameof(utcNow));

        UtcNow = utcNow;
    }
}
