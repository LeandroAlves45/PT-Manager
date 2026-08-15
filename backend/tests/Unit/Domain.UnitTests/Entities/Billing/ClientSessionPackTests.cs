using Domain.Entities.Billing;
using Domain.Exceptions;

namespace Domain.UnitTests.Entities.Billing;

public sealed class ClientSessionPackTests
{
    private static readonly Guid TrainerId = Guid.NewGuid();
    private static readonly Guid ClientId = Guid.NewGuid();
    private static readonly DateTime Now =
        new(2026, 8, 14, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Constructor_PackTypeChanges_PreservesCommercialSnapshot()
    {
        var type = CreateType();
        var pack = CreatePack(type);

        type.Update("Changed", 99, 99999, "USD", 90, Now.AddMinutes(1));

        Assert.Equal("Ten sessions", pack.PackName);
        Assert.Equal(10, pack.SessionsTotal);
        Assert.Equal(10000, pack.PriceCents);
        Assert.Equal("EUR", pack.Currency);
    }

    [Fact]
    public void ExpectedEndDate_InPastButAfterPurchase_RemainsUsable()
    {
        var pack = new ClientSessionPack(
            TrainerId,
            ClientId,
            CreateType(),
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 2, 1),
            Now
        );

        Assert.True(pack.IsUsable);
    }

    [Fact]
    public void Constructor_ExpectedEndDateBeforePurchase_Throws()
    {
        Assert.Throws<DomainException>(() => new ClientSessionPack(
            TrainerId,
            ClientId,
            CreateType(),
            new DateOnly(2026, 2, 1),
            new DateOnly(2026, 1, 31),
            Now
        ));
    }

    [Fact]
    public void ConsumeSession_LastBalance_SetsCompletionState()
    {
        var type = new PackType(
            TrainerId, "One", 1, 1000, "EUR", null, Now
        );
        var pack = CreatePack(type);
        var completedAt = Now.AddHours(1);

        pack.ConsumeSession(completedAt);

        Assert.Equal(0, pack.SessionsRemaining);
        Assert.True(pack.IsCompleted);
        Assert.False(pack.IsUsable);
        Assert.Equal(completedAt, pack.CompletedAt);
    }

    [Fact]
    public void ConsumeSession_EmptyPack_ThrowsWithoutNegativeBalance()
    {
        var type = new PackType(
            TrainerId, "One", 1, 1000, "EUR", null, Now
        );
        var pack = CreatePack(type);
        pack.ConsumeSession(Now.AddMinutes(1));

        Assert.Throws<DomainException>(() => pack.ConsumeSession(Now.AddMinutes(2)));
        Assert.Equal(0, pack.SessionsRemaining);
    }

    [Fact]
    public void RestoreSession_CompletedPack_ClearsCompletionState()
    {
        var type = new PackType(
            TrainerId, "One", 1, 1000, "EUR", null, Now
        );
        var pack = CreatePack(type);
        pack.ConsumeSession(Now.AddMinutes(1));

        pack.RestoreSession(Now.AddMinutes(2));

        Assert.Equal(1, pack.SessionsRemaining);
        Assert.False(pack.IsCompleted);
        Assert.Null(pack.CompletedAt);
    }

    [Fact]
    public void RestoreSession_FullPack_ThrowsWithoutExceedingTotal()
    {
        var pack = CreatePack(CreateType());

        Assert.Throws<DomainException>(() => pack.RestoreSession(Now.AddMinutes(1)));
        Assert.Equal(pack.SessionsTotal, pack.SessionsRemaining);
    }

    [Fact]
    public void ChangeExpectedEndDate_ValidDate_PreservesBalanceAndCompletion()
    {
        var pack = CreatePack(CreateType());

        pack.ChangeExpectedEndDate(new DateOnly(2027, 1, 1), Now.AddMinutes(1));

        Assert.Equal(10, pack.SessionsRemaining);
        Assert.Null(pack.CompletedAt);
    }

    [Fact]
    public void Cancel_RepeatedOnUnusedPack_PreservesFirstTransitionTimestamp()
    {
        var pack = CreatePack(CreateType());
        var cancelledAt = Now.AddMinutes(1);

        pack.Cancel(cancelledAt);
        pack.Cancel(Now.AddMinutes(2));

        Assert.True(pack.IsDeleted);
        Assert.Equal(cancelledAt, pack.UpdatedAt);
    }

    [Fact]
    public void Cancel_UsedPack_Throws()
    {
        var pack = CreatePack(CreateType());
        pack.ConsumeSession(Now.AddMinutes(1));

        Assert.Throws<DomainException>(() => pack.Cancel(Now.AddMinutes(2)));
    }

    [Fact]
    public void Constructor_OtherTenantPackType_Throws()
    {
        var otherType = new PackType(
            Guid.NewGuid(), "Pack", 10, 10000, "EUR", null, Now
        );

        Assert.Throws<DomainException>(() => new ClientSessionPack(
            TrainerId,
            ClientId,
            otherType,
            new DateOnly(2026, 8, 14),
            null,
            Now
        ));
    }

    [Fact]
    public void Constructor_InactivePackType_Throws()
    {
        var type = CreateType();
        type.Archive(Now.AddMinutes(1));

        Assert.Throws<DomainException>(() => new ClientSessionPack(
            TrainerId,
            ClientId,
            type,
            new DateOnly(2026, 8, 14),
            null,
            Now
        ));
    }

    private static PackType CreateType() => new(
        TrainerId, "Ten sessions", 10, 10000, "EUR", 30, Now
    );

    private static ClientSessionPack CreatePack(PackType type) => new(
        TrainerId,
        ClientId,
        type,
        new DateOnly(2026, 8, 14),
        new DateOnly(2026, 9, 13),
        Now
    );
}
