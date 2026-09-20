using Application.Features.Clients;
using Application.Features.Clients.Dtos;
using Application.Features.Clients.GetClientSummary;

namespace Application.UnitTests.Features;

public sealed class GetClientSummaryHandlerTests : ReadHandlerTestContext
{
    [Fact]
    public async Task ClientSummary_WhenClientIsUnknown_ReturnsNotFound()
    {
        var handler = new GetClientSummaryHandler(
            new TenantStub(TrainerId, TrainerId, "trainer"),
            new ClockStub(NowUtc),
            new TimeZoneStub(Lisbon),
            new ClientSummaryQueriesFake { Snapshot = null });

        var result = await handler.HandleAsync(new GetClientSummaryQuery(ClientId), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ClientErrors.ClientNotFound.Code, result.Error!.Code);
    }

    [Fact]
    public async Task ClientSummary_ComputesWeightChangeAndAdherenceWindow()
    {
        var queries = new ClientSummaryQueriesFake
        {
            Snapshot = new ClientProgressSnapshot(
                ClientId,
                new CheckInWeightRow(new DateOnly(2026, 9, 1), 64.8m),
                new CheckInWeightRow(new DateOnly(2026, 8, 4), 66.2m),
                new InitialAssessmentRow(70m, 168, new DateTime(2026, 7, 1, 8, 0, 0, DateTimeKind.Utc)),
                new ActiveTrainingPlanStructure(
                    Guid.NewGuid(),
                    "Força 3x",
                    new DateOnly(2026, 8, 31),
                    null,
                    [
                        new PlannedDayRow(1, 0, [new PlannedSetRow(PrescriptionId, 1)]),
                        new PlannedDayRow(1, 2, [new PlannedSetRow(PrescriptionId, 1)])
                    ]),
                [
                    new PerformedSetRow(
                        PrescriptionId,
                        1,
                        new DateTimeOffset(2026, 8, 31, 10, 0, 0, TimeSpan.Zero))
                ],
                null,
                new ClientProgressSummaryDto.PacksDto(1, 3, new DateOnly(2026, 9, 30)))
        };

        var handler = new GetClientSummaryHandler(
            new TenantStub(TrainerId, TrainerId, "trainer"),
            new ClockStub(NowUtc),
            new TimeZoneStub(Lisbon),
            queries);

        var result = await handler.HandleAsync(new GetClientSummaryQuery(ClientId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(ClientProgressSummaryDto.WeightSourceCheckIn, result.Value.Weight!.Source);
        Assert.Equal(-1.4m, result.Value.Weight.ChangeKg);
        Assert.Equal(new DateOnly(2026, 8, 4), result.Value.Weight.ChangeSince);
        Assert.Equal(168, result.Value.HeightCm);
        Assert.Equal(2, result.Value.TrainingPlan!.DaysPerWeek);
        // A janela começa no início do plano, não 28 dias antes de hoje.
        Assert.Equal(new DateOnly(2026, 8, 31), result.Value.Adherence!.WindowStart);
        Assert.Equal(new DateOnly(2026, 9, 3), result.Value.Adherence.WindowEnd);
        Assert.Equal(1, result.Value.Adherence.LoggedSets);
        // 56 dias antes de 2026-09-03 (janela do "−1,4 kg em 8 semanas").
        Assert.Equal(new DateOnly(2026, 7, 9), queries.LastWeightWindowStart);
    }

    [Fact]
    public async Task ClientSummary_WithoutCheckIns_FallsBackToInitialAssessment()
    {
        var handler = new GetClientSummaryHandler(
            new TenantStub(TrainerId, TrainerId, "trainer"),
            new ClockStub(NowUtc),
            new TimeZoneStub(Lisbon),
            new ClientSummaryQueriesFake
            {
                Snapshot = new ClientProgressSnapshot(
                    ClientId,
                    null,
                    null,
                    new InitialAssessmentRow(70m, 168, new DateTime(2026, 7, 1, 8, 0, 0, DateTimeKind.Utc)),
                    null,
                    [],
                    null,
                    new ClientProgressSummaryDto.PacksDto(0, 0, null))
            });

        var result = await handler.HandleAsync(new GetClientSummaryQuery(ClientId), CancellationToken.None);

        Assert.Equal(
            ClientProgressSummaryDto.WeightSourceInitialAssessment, result.Value.Weight!.Source);
        Assert.Equal(70m, result.Value.Weight.CurrentKg);
        Assert.Null(result.Value.Weight.ChangeKg);
        Assert.Null(result.Value.Adherence);
    }

    [Fact]
    public async Task ClientSummary_WithActivePlanAndNoPlannedSets_ReturnsZeroCountsAndNullPercentage()
    {
        var queries = new ClientSummaryQueriesFake
        {
            Snapshot = new ClientProgressSnapshot(
                ClientId,
                null,
                null,
                null,
                new ActiveTrainingPlanStructure(
                    Guid.NewGuid(), "Plano vazio", new DateOnly(2026, 8, 31), null, []),
                [],
                null,
                new ClientProgressSummaryDto.PacksDto(0, 0, null))
        };
        var handler = new GetClientSummaryHandler(
            new TenantStub(TrainerId, TrainerId, "trainer"),
            new ClockStub(NowUtc),
            new TimeZoneStub(Lisbon),
            queries);

        var result = await handler.HandleAsync(
            new GetClientSummaryQuery(ClientId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.Adherence);
        Assert.Equal(0, result.Value.Adherence.PlannedSets);
        Assert.Equal(0, result.Value.Adherence.LoggedSets);
        Assert.Null(result.Value.Adherence.Percentage);
    }

}
