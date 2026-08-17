using Domain.Entities.Sessions;
using Domain.Exceptions;
using Domain.ValueObjects;

namespace Domain.UnitTests.Entities.Sessions;

public sealed class SessionTests
{
    private static readonly DateTime Now =
        new(2026, 8, 16, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Constructor_NormalizesStartAndFields()
    {
        var session = CreateSession(
            new DateTimeOffset(2026, 8, 17, 12, 0, 0, TimeSpan.FromHours(2)));

        Assert.Equal(TimeSpan.Zero, session.StartsAt.Offset);
        Assert.Equal("Gym", session.Location);
        Assert.Equal(SessionStatus.Scheduled, session.Status);
    }

    [Fact]
    public void Reschedule_Equivalent_DoesNotChangeUpdatedAt()
    {
        var session = CreateSession();

        session.Reschedule(session.StartsAt, 60, " Gym ", Now.AddMinutes(1));

        Assert.Equal(Now, session.UpdatedAt);
    }

    [Fact]
    public void ChangePack_Null_RemovesPackWithoutChangingStatus()
    {
        var session = CreateSession();

        session.ChangePack(null, Now.AddMinutes(1));

        Assert.Null(session.ClientSessionPackId);
        Assert.Equal(SessionStatus.Scheduled, session.Status);
    }

    [Fact]
    public void Complete_Repeated_IsIdempotent()
    {
        var session = CreateSession();
        session.Complete(Now.AddDays(2));
        var changedAt = session.StatusChangedAt;

        session.Complete(Now.AddDays(3));

        Assert.Equal(changedAt, session.StatusChangedAt);
    }

    [Fact]
    public void CancelByClient_PersistsDetailedStatus()
    {
        var session = CreateSession();

        session.CancelByClient(Now.AddMinutes(1));

        Assert.Equal(SessionStatus.CancelledByClient, session.Status);
    }

    [Fact]
    public void CancelByTrainer_PersistsDetailedStatus()
    {
        var session = CreateSession();

        session.CancelByTrainer(Now.AddMinutes(1));

        Assert.Equal(SessionStatus.CancelledByTrainer, session.Status);
    }

    [Fact]
    public void DifferentTerminalTransition_Throws()
    {
        var session = CreateSession();
        session.Complete(Now.AddDays(2));

        var action = () => session.MarkNoShow(Now.AddDays(2).AddMinutes(1));

        Assert.Throws<DomainException>(action);
    }

    [Theory]
    [InlineData("completed")]
    [InlineData("cancelled_by_client")]
    [InlineData("cancelled_by_trainer")]
    [InlineData("no_show")]
    public void Restore_TerminalState_ReturnsScheduled(string state)
    {
        var session = CreateSession();
        ApplyState(session, state);

        session.Restore(Now.AddDays(3));

        Assert.Equal(SessionStatus.Scheduled, session.Status);
    }

    [Fact]
    public void Restore_Scheduled_DoesNotChangeTimestamps()
    {
        var session = CreateSession();

        session.Restore(Now.AddDays(3));

        Assert.Equal(Now, session.UpdatedAt);
        Assert.Equal(Now, session.StatusChangedAt);
    }

    [Fact]
    public void Constructor_InvalidDuration_Throws()
    {
        var action = () => new Session(
            Guid.NewGuid(), Guid.NewGuid(), null, DateTimeOffset.UtcNow,
            0, null, null, null, Now);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void Constructor_EmptyPackId_Throws()
    {
        var action = () => new Session(
            Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, DateTimeOffset.UtcNow,
            60, null, null, null, Now);

        Assert.Throws<DomainException>(action);
    }

    private static Session CreateSession(DateTimeOffset? start = null) => new(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
        start ?? new DateTimeOffset(Now.AddDays(1)),
        60, " Gym ", " strength ", null, Now);

    private static void ApplyState(Session session, string state)
    {
        var at = Now.AddDays(2);
        if (state == "completed")
            session.Complete(at);
        else if (state == "cancelled_by_client")
            session.CancelByClient(at);
        else if (state == "cancelled_by_trainer")
            session.CancelByTrainer(at);
        else if (state == "no_show")
            session.MarkNoShow(at);
        else
            throw new ArgumentOutOfRangeException(nameof(state));
    }
}
