using Domain.Entities.Identity;
using Domain.Exceptions;
using Domain.ValueObjects;
using Xunit;

namespace Unit.Domain.UnitTests.Entities.Identity;

public sealed class UserTests
{
    [Fact]
    public void SetPasswordHash_ValidHash_AssignsHash()
    {
        // Arrange
        var now = new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);
        var email = new EmailAddress("johnDoe@example.com");
        var user = new User(email, "trainer", "Jonh Doe", now);
        var expectedHash = "hash-value-for-identity";

        // Act
        user.SetPasswordHash(expectedHash, now);

        // Assert
        Assert.Equal(expectedHash, user.PasswordHash);
        Assert.Equal(now, user.UpdatedAt);
    }

    [Fact]
    public void SetEmail_ConfirmedUser_ResetsConfirmation()
    {
        // Arrange
        var now = new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);
        var email = new EmailAddress("johnDoe@example.com");
        var user = new User(email, "trainer", "John Doe", now);
        user.ConfirmEmail(now);

        // Act
        var newEmail = new EmailAddress("test@example.com");
        user.SetEmail(newEmail, now);

        // Assert
        Assert.False(user.EmailConfirmed);
    }

    /// <summary>
    /// PTM-SEC-18: o UserManager define o lockout e chama o reset do contador logo a
    /// seguir. O reset não pode apagar o bloqueio acabado de definir.
    /// </summary>
    [Fact]
    public void ResetAccessFailedCount_LockedUser_ClearsCounterAndPreservesLockoutEnd()
    {
        // Arrange
        var now = new DateTime(2026, 9, 14, 12, 0, 0, DateTimeKind.Utc);
        var user = new User(new EmailAddress("john@example.com"), "trainer", "John Doe", now);
        user.RegisterFailedAccess(now);
        user.RegisterFailedAccess(now);
        var lockoutEnd = now.AddMinutes(15);
        user.SetLockoutEnd(lockoutEnd, now);

        // Act
        user.ResetAccessFailedCount(now.AddSeconds(1));

        // Assert
        Assert.Equal(0, user.AccessFailedCount);
        Assert.Equal(lockoutEnd, user.LockoutEnd);
    }

    [Fact]
    public void SetEmail_DeletedUser_ThrowsDomainException()
    {
        // Arrange
        var now = new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);
        var user = new User(new EmailAddress("john@example.com"), "trainer", "John Doe", now);
        user.SoftDelete(now);

        // Act & Assert
        Assert.Throws<DomainException>(() =>
            user.SetEmail(new EmailAddress("new@example.com"), now.AddMinutes(1)));
    }

    [Fact]
    public void DeleteAndAnonymize_RemovesCredentialsAndIdentity_AndFreesTheEmail()
    {
        var now = new DateTime(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc);
        var user = new User(new EmailAddress("cliente@example.com"), "client", "Ana Silva", now);
        user.SetPasswordHash("hash", now);
        user.ConfirmEmail(now);
        user.RegisterFailedAccess(now);
        var stamp = user.SecurityStamp;

        user.DeleteAndAnonymize(now.AddMinutes(1));

        Assert.Equal($"deleted-{user.Id:N}@deleted.invalid", user.Email);
        Assert.Equal(user.Email.ToUpperInvariant(), user.NormalizedEmail);
        Assert.DoesNotContain("CLIENTE@EXAMPLE.COM", user.NormalizedEmail);
        Assert.Null(user.PasswordHash);
        Assert.Null(user.FullName);
        Assert.False(user.EmailConfirmed);
        Assert.Equal(0, user.AccessFailedCount);
        Assert.NotEqual(stamp, user.SecurityStamp);
        Assert.True(user.IsDeleted);
        Assert.False(user.IsActive);
    }

    [Fact]
    public void DeleteAndAnonymize_Twice_Throws()
    {
        var now = new DateTime(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc);
        var user = new User(new EmailAddress("cliente2@example.com"), "client", null, now);
        user.DeleteAndAnonymize(now);

        Assert.Throws<DomainException>(() => user.DeleteAndAnonymize(now));
    }
}
