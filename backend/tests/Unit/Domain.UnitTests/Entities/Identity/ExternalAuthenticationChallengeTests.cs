using Domain.Entities.Identity;
using Domain.Exceptions;
using Xunit;

namespace Unit.Domain.UnitTests.Entities.Identity;

/// <summary>Finalidade, ator, hash e expiração de <see cref="ExternalAuthenticationChallenge"/>.</summary>
public sealed class ExternalAuthenticationChallengeTests
{
    private const string Hash =
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
    private static readonly DateTime Now =
        new(2026, 9, 3, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Constructor_SignInWithoutUser_Succeeds()
    {
        var challenge = new ExternalAuthenticationChallenge(
            Hash, ExternalAuthenticationChallenge.SignInPurpose, null,
            Now.AddMinutes(5), Now);

        Assert.Null(challenge.UserId);
        Assert.Equal(ExternalAuthenticationChallenge.SignInPurpose, challenge.Purpose);
        Assert.Equal(Hash, challenge.NonceHash);
        Assert.Equal(Now, challenge.CreatedAt);
    }

    [Fact]
    public void Constructor_LinkWithUser_Succeeds()
    {
        var userId = Guid.NewGuid();

        var challenge = new ExternalAuthenticationChallenge(
            Hash, ExternalAuthenticationChallenge.LinkPurpose, userId,
            Now.AddMinutes(5), Now);

        Assert.Equal(userId, challenge.UserId);
        Assert.Equal(ExternalAuthenticationChallenge.LinkPurpose, challenge.Purpose);
    }

    [Fact]
    public void Constructor_LowercaseHash_IsNormalizedToUpperCase()
    {
        var challenge = new ExternalAuthenticationChallenge(
            Hash.ToLowerInvariant(), ExternalAuthenticationChallenge.SignInPurpose, null,
            Now.AddMinutes(5), Now);

        Assert.Equal(Hash, challenge.NonceHash);
    }

    [Fact]
    public void Constructor_LinkWithoutUser_Throws()
    {
        Assert.Throws<DomainException>(() => new ExternalAuthenticationChallenge(
            Hash, ExternalAuthenticationChallenge.LinkPurpose, null,
            Now.AddMinutes(5), Now));
    }

    [Fact]
    public void Constructor_SignInWithUser_Throws()
    {
        Assert.Throws<DomainException>(() => new ExternalAuthenticationChallenge(
            Hash, ExternalAuthenticationChallenge.SignInPurpose, Guid.NewGuid(),
            Now.AddMinutes(5), Now));
    }

    [Fact]
    public void Constructor_UnknownPurpose_Throws()
    {
        Assert.Throws<DomainException>(() => new ExternalAuthenticationChallenge(
            Hash, "reset", null, Now.AddMinutes(5), Now));
    }

    [Theory]
    [InlineData("")]
    [InlineData("ZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZ")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public void Constructor_InvalidHash_Throws(string hash)
    {
        // 63 hexadecimais, 64 não hexadecimais e vazio falham todos: o hash tem de ser
        // exatamente um SHA-256 em hexadecimal.
        Assert.Throws<DomainException>(() => new ExternalAuthenticationChallenge(
            hash, ExternalAuthenticationChallenge.SignInPurpose, null,
            Now.AddMinutes(5), Now));
    }

    [Fact]
    public void Constructor_ExpirationNotInFuture_Throws()
    {
        Assert.Throws<DomainException>(() => new ExternalAuthenticationChallenge(
            Hash, ExternalAuthenticationChallenge.SignInPurpose, null, Now, Now));
    }

    [Fact]
    public void Constructor_NonUtcExpiration_Throws()
    {
        Assert.Throws<DomainException>(() => new ExternalAuthenticationChallenge(
            Hash,
            ExternalAuthenticationChallenge.SignInPurpose,
            null,
            new DateTime(2026, 9, 3, 12, 5, 0, DateTimeKind.Local),
            Now));
    }

    [Fact]
    public void IsExpired_AtExpiration_ReturnsTrue()
    {
        // O limite é inclusivo para falhar fechado no instante exato da expiração.
        var expires = Now.AddMinutes(5);
        var challenge = new ExternalAuthenticationChallenge(
            Hash, ExternalAuthenticationChallenge.SignInPurpose, null, expires, Now);

        Assert.True(challenge.IsExpired(expires));
        Assert.False(challenge.IsExpired(expires.AddTicks(-1)));
    }
}
