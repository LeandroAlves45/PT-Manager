using Application.Features.Assessments.CheckIns.Dtos;
using Application.Features.ClientPortal;
using Application.Features.ClientPortal.Dtos;
using Application.Features.ClientPortal.GetMyPortalHome;
using Application.Features.Supplements.Dtos;

namespace Application.UnitTests.Features;

public sealed class GetMyPortalHomeHandlerTests : ReadHandlerTestContext
{
    [Fact]
    public async Task PortalHome_WhenClientIsUnknown_ReturnsProfileNotAvailable()
    {
        var handler = CreateHomeHandler(new IntakeQueriesFake { Result = null });

        var result = await handler.HandleAsync(new GetMyPortalHomeQuery(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ClientPortalErrors.ProfileNotAvailable.Code, result.Error!.Code);
    }

    [Fact]
    public async Task PortalHome_ComposesEveryCard()
    {
        var handler = CreateHomeHandler(
            new IntakeQueriesFake
            {
                Result = new MyTodaySupplementIntakesDto(new DateOnly(2026, 9, 3), 1, 3, [])
            },
            nextCheckIn: new MyNextCheckInDto(Guid.NewGuid(), new DateOnly(2026, 9, 5), false));

        var result = await handler.HandleAsync(new GetMyPortalHomeQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new DateOnly(2026, 9, 3), result.Value.LocalDate);
        Assert.Equal(MyWorkoutTodayStatus.Workout, result.Value.Workout!.Status);
        Assert.Equal(2, result.Value.Workout.PlannedSets);
        Assert.Equal(1, result.Value.Nutrition!.MealCount);
        Assert.Equal(1, result.Value.Supplements.TakenCount);
        Assert.Equal(3, result.Value.Supplements.TotalCount);
        Assert.False(result.Value.NextCheckIn!.IsToday);
    }

}
