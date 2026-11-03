using Application.Features.ClientPortal.GetMyTrainingPlan;
using Application.Features.ClientPortal.GetMyWorkoutToday;

namespace Application.UnitTests.Features;

/// <summary>
/// Uma ligação de vídeo gravada antes da regra atual (qualquer HTTPS) nunca chega ao cliente:
/// o plano e o treino de hoje só enviam links do YouTube ou do Vimeo.
/// </summary>
public sealed class PortalExternalVideoLinkTests : ReadHandlerTestContext
{
    private const string LegacyLink = "https://videos.example/agachamento";

    [Theory]
    [InlineData(ExerciseVideoUrl, ExerciseVideoUrl)]
    [InlineData(LegacyLink, null)]
    [InlineData("https://youtube.com.evil.example/watch?v=x", null)]
    public async Task TrainingPlan_SendsOnlyAllowedLinks(string stored, string? sent)
    {
        var handler = new GetMyTrainingPlanHandler(
            new TenantStub(TrainerId, ClientUserId, "client"),
            new TrainingPlanQueriesFake { Plan = PlanWith(dayOfWeek: 3, videoUrl: stored) });

        var result = await handler.HandleAsync(CancellationToken.None);

        Assert.Equal(sent, Assert.Single(Assert.Single(result.Value.Days).Exercises).VideoUrl);
    }

    [Fact]
    public async Task WorkoutToday_DropsALegacyLink()
    {
        var handler = new GetMyWorkoutTodayHandler(
            new TenantStub(TrainerId, ClientUserId, "client"),
            new ClockStub(NowUtc),
            new TimeZoneStub(Lisbon),
            new MyWorkoutTodayReader(
                new TrainingPlanQueriesFake { Plan = PlanWith(dayOfWeek: 3, videoUrl: LegacyLink) },
                new WorkoutTodayQueriesFake()));

        var result = await handler.HandleAsync(new GetMyWorkoutTodayQuery(), CancellationToken.None);

        Assert.Null(Assert.Single(result.Value.Day!.Exercises).VideoUrl);
    }
}
