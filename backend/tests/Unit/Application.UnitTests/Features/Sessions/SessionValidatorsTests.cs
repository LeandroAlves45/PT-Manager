using Application.Features.Sessions.ChangeSessionPack;
using Application.Features.Sessions.CreateSession;
using Application.Features.Sessions.ListSessions;
using Application.Features.Sessions.RescheduleSession;

namespace Application.UnitTests.Features.Sessions;

public sealed class SessionValidatorsTests
{
    private static readonly DateTime Now =
        new(2026, 8, 16, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Create_StartAtCurrentInstant_IsInvalid()
    {
        var validator = new CreateSessionCommandValidator(new ClockStub());
        var command = new CreateSessionCommand(
            Guid.NewGuid(),
            null,
            new DateTimeOffset(Now),
            60,
            null,
            null,
            null);

        var result = await validator.ValidateAsync(
            command,
            TestContext.Current.CancellationToken);

        Assert.Contains(
            result.Errors,
            failure => failure.ErrorCode == "session_starts_at_not_future");
    }

    [Fact]
    public async Task Reschedule_StartAtCurrentInstant_IsInvalid()
    {
        var validator = new RescheduleSessionCommandValidator(new ClockStub());
        var command = new RescheduleSessionCommand(
            Guid.NewGuid(),
            new DateTimeOffset(Now),
            60,
            null);

        var result = await validator.ValidateAsync(
            command,
            TestContext.Current.CancellationToken);

        Assert.Contains(
            result.Errors,
            failure => failure.ErrorCode == "session_starts_at_not_future");
    }

    [Fact]
    public async Task ChangePack_EmptyOptionalPackId_IsInvalid()
    {
        var validator = new ChangeSessionPackCommandValidator();

        var result = await validator.ValidateAsync(
            new ChangeSessionPackCommand(Guid.NewGuid(), Guid.Empty),
            TestContext.Current.CancellationToken);

        Assert.Contains(
            result.Errors,
            failure => failure.ErrorCode == "client_session_pack_id_invalid");
    }

    [Fact]
    public async Task List_EndNotAfterStart_IsInvalid()
    {
        var validator = new ListSessionsQueryValidator();
        var boundary = new DateTimeOffset(Now);

        var result = await validator.ValidateAsync(
            new ListSessionsQuery(null, null, boundary, boundary),
            TestContext.Current.CancellationToken);

        Assert.Contains(
            result.Errors,
            failure => failure.ErrorCode == "session_date_range_invalid");
    }

    private sealed class ClockStub : Application.Common.Abstractions.IClock
    {
        public DateTime UtcNow => Now;
    }
}
