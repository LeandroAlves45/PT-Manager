using Domain.Services;

namespace Domain.UnitTests.Services;

/// <summary>
/// Prova a fórmula de adesão da Sprint 6B: só contam séries feitas no dia em que o
/// calendário cíclico as previa, nunca ultrapassa 100 % e ignora dias fora da janela.
/// </summary>
public sealed class TrainingAdherenceCalculatorTests
{
    private static readonly Guid Prescription = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateOnly Monday = new(2026, 8, 31);

    private static IReadOnlyList<PlannedTrainingDay> OneMondayWithTwoSets() =>
    [
        new PlannedTrainingDay(1, 0, [
            new PlannedSetRef(Prescription, 1),
            new PlannedSetRef(Prescription, 2)
        ])
    ];

    [Fact]
    public void Calculate_WithoutDays_ReturnsNullPercentage()
    {
        var result = TrainingAdherenceCalculator.Calculate(
            Monday, null, [], [], Monday, Monday.AddDays(6));

        Assert.Equal(0, result.PlannedSets);
        Assert.Null(result.Percentage);
    }

    [Fact]
    public void Calculate_WithNoLogs_IsZeroPercent()
    {
        var result = TrainingAdherenceCalculator.Calculate(
            Monday, null, OneMondayWithTwoSets(), [], Monday, Monday.AddDays(6));

        Assert.Equal(2, result.PlannedSets);
        Assert.Equal(0, result.LoggedSets);
        Assert.Equal(0, result.Percentage);
    }

    [Fact]
    public void Calculate_WithEverySetLogged_IsHundredPercent()
    {
        var result = TrainingAdherenceCalculator.Calculate(
            Monday,
            null,
            OneMondayWithTwoSets(),
            [
                new PerformedSetRef(Prescription, 1, Monday),
                new PerformedSetRef(Prescription, 2, Monday)
            ],
            Monday,
            Monday.AddDays(6));

        Assert.Equal(2, result.LoggedSets);
        Assert.Equal(100, result.Percentage);
    }

    [Fact]
    public void Calculate_WithLogOnUnscheduledDay_DoesNotCount()
    {
        // Registo feito na terça, quando o plano só prevê segunda: não conta.
        var result = TrainingAdherenceCalculator.Calculate(
            Monday,
            null,
            OneMondayWithTwoSets(),
            [new PerformedSetRef(Prescription, 1, Monday.AddDays(1))],
            Monday,
            Monday.AddDays(6));

        Assert.Equal(2, result.PlannedSets);
        Assert.Equal(0, result.LoggedSets);
    }

    [Fact]
    public void Calculate_WithDuplicatedLogs_NeverExceedsOneHundred()
    {
        var result = TrainingAdherenceCalculator.Calculate(
            Monday,
            null,
            OneMondayWithTwoSets(),
            [
                new PerformedSetRef(Prescription, 1, Monday),
                new PerformedSetRef(Prescription, 1, Monday),
                new PerformedSetRef(Prescription, 2, Monday)
            ],
            Monday,
            Monday.AddDays(6));

        Assert.Equal(2, result.LoggedSets);
        Assert.Equal(100, result.Percentage);
    }

    [Fact]
    public void Calculate_RepeatsCycleAcrossWeeks()
    {
        // Plano de uma semana: a janela de duas semanas prevê o dobro das séries.
        var result = TrainingAdherenceCalculator.Calculate(
            Monday, null, OneMondayWithTwoSets(), [], Monday, Monday.AddDays(13));

        Assert.Equal(4, result.PlannedSets);
    }

    [Fact]
    public void Calculate_StopsAtPlanEnd()
    {
        var result = TrainingAdherenceCalculator.Calculate(
            Monday,
            Monday.AddDays(6),
            OneMondayWithTwoSets(),
            [],
            Monday,
            Monday.AddDays(13));

        Assert.Equal(2, result.PlannedSets);
    }

    [Fact]
    public void Calculate_RoundsHalfAwayFromZero()
    {
        var days = new List<PlannedTrainingDay>
        {
            new(1, 0, [
                new PlannedSetRef(Prescription, 1),
                new PlannedSetRef(Prescription, 2),
                new PlannedSetRef(Prescription, 3)
            ])
        };

        var result = TrainingAdherenceCalculator.Calculate(
            Monday,
            null,
            days,
            [new PerformedSetRef(Prescription, 1, Monday)],
            Monday,
            Monday.AddDays(6));

        // 1 de 3 = 33,33 % -> 33.
        Assert.Equal(33, result.Percentage);
    }
}
