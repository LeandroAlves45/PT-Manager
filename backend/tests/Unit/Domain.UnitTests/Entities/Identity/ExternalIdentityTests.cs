using Domain.Entities.Identity;
using Domain.Exceptions;
using Xunit;

namespace Unit.Domain.UnitTests.Entities.Identity;

/// <summary>Invariantes de <see cref="ExternalIdentity"/> sem infraestrutura nem mocks.</summary>
public sealed class ExternalIdentityTests
{
    private static readonly DateTime Now =
        new(2026, 9, 3, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Constructor_ValidGoogleIdentity_NormalizesProvider()
    {
        var userId = Guid.NewGuid();

        var identity = new ExternalIdentity(userId, " GOOGLE ", "subject-1", Now);

        Assert.Equal(ExternalIdentity.GoogleProvider, identity.Provider);
        Assert.Equal(userId, identity.UserId);
        Assert.Equal("subject-1", identity.Subject);
        Assert.Equal(Now, identity.CreatedAt);
        Assert.NotEqual(Guid.Empty, identity.Id);
    }

    [Fact]
    public void Constructor_SubjectWithSurroundingWhitespace_IsTrimmed()
    {
        var identity = new ExternalIdentity(Guid.NewGuid(), "google", "  subject-1  ", Now);

        Assert.Equal("subject-1", identity.Subject);
    }

    [Fact]
    public void Constructor_UnsupportedProvider_Throws()
    {
        Assert.Throws<DomainException>(() =>
            new ExternalIdentity(Guid.NewGuid(), "other", "subject-1", Now));
    }

    [Fact]
    public void Constructor_EmptyUser_Throws()
    {
        Assert.Throws<DomainException>(() =>
            new ExternalIdentity(Guid.Empty, "google", "subject-1", Now));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_BlankSubject_Throws(string subject)
    {
        Assert.Throws<DomainException>(() =>
            new ExternalIdentity(Guid.NewGuid(), "google", subject, Now));
    }

    [Fact]
    public void Constructor_OversizedSubject_Throws()
    {
        Assert.Throws<DomainException>(() =>
            new ExternalIdentity(Guid.NewGuid(), "google", new string('s', 256), Now));
    }

    [Fact]
    public void Constructor_NonUtcTimestamp_Throws()
    {
        // O relógio do domínio é sempre UTC; um DateTime local falharia silenciosamente
        // ao ser persistido como timestamptz.
        Assert.Throws<DomainException>(() => new ExternalIdentity(
            Guid.NewGuid(),
            "google",
            "subject-1",
            new DateTime(2026, 9, 3, 12, 0, 0, DateTimeKind.Local)));
    }
}
