using Domain.Entities.Identity;
using Domain.Exceptions;
using Domain.ValueObjects;

namespace Domain.UnitTests.Entities.Identity;

/// <summary>Convite de treinador isento: uso único, 7 dias desde o envio (RN-008).</summary>
public sealed class TrainerInvitationTests
{
    private static readonly DateTime Now = new(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void NewInvitation_ExpiresSevenDaysAfterSending()
    {
        var invitation = Create();

        Assert.Equal(new DateTime(2026, 10, 26, 12, 0, 0, DateTimeKind.Utc), invitation.ExpiresAt);
        Assert.True(invitation.IsValid(Now.AddDays(7).AddSeconds(-1)));
        Assert.False(invitation.IsValid(Now.AddDays(7)));
        Assert.Equal("NOVA@EXEMPLO.PT", invitation.NormalizedEmail);
    }

    [Fact]
    public void UsedInvitation_CannotBeUsedOrRevoked()
    {
        var invitation = Create();
        invitation.MarkUsed(Now.AddHours(1));

        Assert.Throws<DomainException>(() => invitation.MarkUsed(Now.AddHours(2)));
        Assert.False(invitation.Revoke(Now.AddHours(2)));
        Assert.Null(invitation.RevokedAt);
    }

    [Fact]
    public void RevokedInvitation_IsNoLongerValidAndKeepsTheFirstRevocationTime()
    {
        var invitation = Create();

        Assert.True(invitation.Revoke(Now.AddHours(1)));
        Assert.False(invitation.Revoke(Now.AddHours(2)));

        Assert.False(invitation.IsValid(Now.AddHours(3)));
        Assert.Equal(Now.AddHours(1), invitation.RevokedAt);
        Assert.Throws<DomainException>(() => invitation.MarkUsed(Now.AddHours(3)));
    }

    [Fact]
    public void ExpiredInvitation_CannotBeUsed()
    {
        var invitation = Create();

        Assert.Throws<DomainException>(() => invitation.MarkUsed(Now.AddDays(8)));
    }

    private static TrainerInvitation Create() =>
        new(new EmailAddress(" Nova@Exemplo.pt "), "hash", Guid.NewGuid(), Now);
}
