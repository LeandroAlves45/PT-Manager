using Domain.Entities.Sessions;
using Domain.Exceptions;
using Xunit;

namespace Domain.UnitTests.Entities.Sessions;

public sealed class SessionTests
{
    [Fact]
    public void Reschedule_CompletedSession_ThrowsDomainException()
    {
        // Arrange
        var now = new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);
        var sessionDate = new DateOnly(2026, 7, 25);
        var session = new Session(
            Guid.NewGuid(),
            Guid.NewGuid(),
            sessionDate,
            null,
            null,
            null,
            null,
            now
        );

        // Act & Assert
        session.MarkAsCompleted(now);
        var exception = Assert.Throws<DomainException>(() => session.Reschedule(
            new DateOnly(2026, 7, 26),
            null,
            now
        ));
        Assert.Equal("Cannot reschedule a completed session.", exception.Message);
    }

    [Fact]
    public void MarkAsCompleted_Twice_DoesNotChangeUpdatedAt()
    {
        // Arrange
        var now = new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);
        var sessionDate = new DateOnly(2026, 7, 25);
        var session = new Session(
            Guid.NewGuid(),
            Guid.NewGuid(),
            sessionDate,
            null,
            null,
            null,
            null,
            now
        );

        // Act
        session.MarkAsCompleted(now);
        var updatedAtAfterFirstCompletion = session.UpdatedAt;
        session.MarkAsCompleted(now.AddMinutes(5));
        var updatedAtAfterSecondCompletion = session.UpdatedAt;

        // Assert
        Assert.Equal(updatedAtAfterFirstCompletion, updatedAtAfterSecondCompletion);
    }

    [Fact]
    public void SoftDelete_CompletedSession_ThrowsDomainException()
    {
        // Arrange
        var now = new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);
        var sessionDate = new DateOnly(2026, 7, 25);
        var session = new Session(
            Guid.NewGuid(),
            Guid.NewGuid(),
            sessionDate,
            null,
            null,
            null,
            null,
            now
        );

        // Act & Assert
        session.MarkAsCompleted(now);
        var exception = Assert.Throws<DomainException>(() => session.SoftDelete(now));
        Assert.Equal("Cannot delete a completed session.", exception.Message);
    }

    [Fact]
    public void MarkAsCompleted_DeletedSession_ThrowsDomainException()
    {
        // Arrange
        var now = new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);
        var session = new Session(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 7, 25),
            null,
            null,
            null,
            null,
            now
        );
        session.SoftDelete(now);

        // Act & Assert
        Assert.Throws<DomainException>(() => session.MarkAsCompleted(now.AddMinutes(1)));
    }
}
