using Domain.Entities.Notifications;
using Domain.Exceptions;
using Domain.ValueObjects;
using Xunit;

namespace Domain.UnitTests.Entities.Notifications;

public sealed class NotificationTests
{
    [Fact]
    public void MarkSent_FromPending_SetsSentAt()
    {
        // Arrange
        var notification = CreatePendingNotification();
        // Act
        var now = new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);
        notification.MarkSent(now);
        // Assert
        Assert.True(notification.SentAt.HasValue);
        Assert.Equal(now, notification.SentAt.Value);
    }

    [Fact]
    public void MarkBounced_FromPending_ThrowsDomainException()
    {
        // Arrange
        var notification = CreatePendingNotification();

        // Act & Assert
        var now = new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);
        var exception = Assert.Throws<DomainException>(() => notification.MarkBounced(now));
        Assert.Equal("Only one sent notification can be marked as bounced.", exception.Message);
    }

    [Fact]
    public void MarkFailed_FromPending_IncrementRetryCount()
    {
        // Arrange
        var notification = CreatePendingNotification();

        // Act
        var now = new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);
        notification.MarkFailed("sanitized error message", now);

        // Assert
        Assert.Equal(1, notification.RetryCount);
        Assert.Equal("failed", notification.Status);
    }

    [Fact]
    public void RequeueForRetry_FromFailed_ReturnsToPending()
    {
        // Arrange
        var notification = CreatePendingNotification();
        var now = new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);
        notification.MarkFailed("sanitized error message", now);

        // Act
        notification.RequeueForRetry(now);

        // Assert
        Assert.Equal("pending", notification.Status);
    }

    [Fact]
    public void SoftDelete_SendNotification_ThrowsDomainException()
    {
        // Arrange
        var notification = CreatePendingNotification();

        // Act & Assert
        var now = new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);
        notification.MarkSent(now);
        var exception = Assert.Throws<DomainException>(() => notification.SoftDelete(now));
        Assert.Equal("Cannot delete a sent notification.", exception.Message);
    }

    [Fact]
    public void MarkFailed_DeletedNotification_ThrowsDomainException()
    {
        // Arrange
        var now = new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);
        var notification = CreatePendingNotification();
        notification.SoftDelete(now);

        // Act & Assert
        Assert.Throws<DomainException>(() =>
            notification.MarkFailed("sanitized error message", now.AddMinutes(1)));
    }

    /// <summary>Cria uma notificação com status Pending para testes.</summary>
    private static Notification CreatePendingNotification()
    {
        var now = new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);
        var email = new EmailAddress("test@example.com");
        return new Notification(
            Guid.NewGuid(),
            Guid.NewGuid(),
            email,
            "Test Subject",
            "Test Body",
            null,
            now
        );
    }
}
