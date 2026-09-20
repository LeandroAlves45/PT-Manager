using Application.Features.Assessments.CheckIns.Dtos;
using Application.Features.Assessments.CheckIns.GetMyNextCheckIn;

namespace Application.UnitTests.Features;

public sealed class GetMyNextCheckInHandlerTests : ReadHandlerTestContext
{
    [Fact]
    public async Task NextCheckIn_WithTrainerActor_IsForbidden()
    {
        var handler = new GetMyNextCheckInHandler(
            new TenantStub(TrainerId, TrainerId, "trainer"),
            new ClockStub(NowUtc),
            new TimeZoneStub(Lisbon),
            new CheckInQueriesFake());

        var result = await handler.HandleAsync(new GetMyNextCheckInQuery(), CancellationToken.None);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task NextCheckIn_UsesLocalTodayOfTrainer()
    {
        var queries = new CheckInQueriesFake
        {
            Next = new MyNextCheckInDto(Guid.NewGuid(), new DateOnly(2026, 9, 3), true)
        };
        var handler = new GetMyNextCheckInHandler(
            new TenantStub(TrainerId, ClientUserId, "client"),
            new ClockStub(NowUtc),
            new TimeZoneStub(Lisbon),
            queries);

        var result = await handler.HandleAsync(new GetMyNextCheckInQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new DateOnly(2026, 9, 3), queries.LastLocalToday);
        Assert.True(result.Value!.IsToday);
    }

    [Fact]
    public async Task NextCheckIn_WithoutScheduledCheckIn_ReturnsSuccessWithNullValue()
    {
        var handler = new GetMyNextCheckInHandler(
            new TenantStub(TrainerId, ClientUserId, "client"),
            new ClockStub(NowUtc),
            new TimeZoneStub(Lisbon),
            new CheckInQueriesFake());

        var result = await handler.HandleAsync(
            new GetMyNextCheckInQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
    }

}
