using Domain.Entities.Sessions;
using Domain.Exceptions;
using Domain.ValueObjects;
using Xunit;

namespace Domain.UnitTests.Entities.Sessions;

public sealed class SessionTests
{
    private static readonly DateTime Now = new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Constructor_StoresStartAsUtc()
    {
        // Arrange
        var session = CreateSession();

        // Assert
        Assert.Equal(DateTimeKind.Utc, session.StartsAt.Kind);
    }

    [Fact]
    public void Complete_WhenScheduled_ChangesStatus()
    {
        // Arrange
        var session = CreateSession();

        // Act
        session.Complete(Now.AddHours(2));

        // Assert
        Assert.Equal(SessionStatus.Completed, session.Status);
    }

    [Fact]
    public void CancelByTrainer_WhenScheduled_RecordsDetailedStatus()
    {
        // Arrange
        var session = CreateSession();

        // Act
        session.CancelByTrainer(Now.AddMinutes(10));

        // Assert
        Assert.Equal(SessionStatus.CancelledByTrainer, session.Status);
    }

    [Fact]
    public void MarkNoShow_WhenAlreadyCompleted_ThrowsDomainException()
    {
        // Arrange
        var session = CreateSession();
        session.Complete(Now.AddHours(2));

        // Act & Assert
        var action = () => session.MarkNoShow(Now.AddHours(3));
        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void Reschedule_WhenStartHasNoTimezone_ThrowsDomainException()
    {
        // Arrange
        var session = CreateSession();
        var unspecified = DateTime.SpecifyKind(Now.AddDays(2), DateTimeKind.Unspecified);

        // Act & Assert
        var action = () => session.Reschedule(unspecified, 60, null, Now.AddMinutes(1));
        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void Reschedule_WhenSessionIsCompleted_ThrowsDomainException()
    {
        var session = CreateSession();
        session.Complete(Now.AddHours(2));

        var action = () => session.Reschedule(
            Now.AddDays(2), 60, null, Now.AddHours(3));

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void SoftDelete_WhenSessionIsCompleted_ThrowsDomainException()
    {
        var session = CreateSession();
        session.Complete(Now.AddHours(2));

        var action = () => session.SoftDelete(Now.AddHours(3));

        Assert.Throws<DomainException>(action);
    }

    private static Session CreateSession() => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        Now.AddDays(1),
        60,
        "Gym A",
        "strength",
        null,
        Now
    );
}
