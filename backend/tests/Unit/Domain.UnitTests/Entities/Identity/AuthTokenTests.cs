using Domain.Entities.Identity;
using Domain.Exceptions;
using Domain.ValueObjects;

namespace Domain.UnitTests.Entities.Identity;

public sealed class AuthTokenTests
{
    private static readonly DateTime Now = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
    private const string Hash = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

    [Fact]
    public void EmailVerificationToken_SecondConsumption_IsRejected()
    {
        var token = new EmailVerificationToken(Guid.NewGuid(), Hash, Now.AddHours(1), Now);
        token.MarkConsumed(Now.AddMinutes(1));

        Assert.Throws<DomainException>(() => token.MarkConsumed(Now.AddMinutes(2)));
    }

    [Fact]
    public void PasswordResetToken_Expired_CannotBeConsumed()
    {
        var token = new PasswordResetToken(Guid.NewGuid(), Hash, Now.AddMinutes(1), Now);

        Assert.False(token.CanConsume(Now.AddMinutes(1)));
        Assert.Throws<DomainException>(() => token.MarkConsumed(Now.AddMinutes(1)));
    }

    [Fact]
    public void InviteToken_StoresOnlyProvidedHashAndNormalizesEmail()
    {
        var token = new InviteToken(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new EmailAddress("client@example.test"),
            Hash,
            Now.AddDays(1),
            Now);

        Assert.Equal(Hash, token.TokenHash);
        Assert.Equal("client@example.test", token.Email);
        Assert.True(token.IsValid(Now));
    }

    [Fact]
    public void RefreshToken_Revocation_IsIdempotent()
    {
        var token = new RefreshToken(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Hash,
            null,
            Now.AddDays(30),
            Now);

        token.Revoke(Now.AddMinutes(1));
        token.Revoke(Now.AddMinutes(2));

        Assert.Equal(Now.AddMinutes(1), token.RevokedAt);
        Assert.True(token.IsReused());
    }
}
