using Domain.Services;

namespace Domain.UnitTests.Services;

/// <summary>
/// Prova o calendário cíclico da Sprint 6B: semanas alinhadas à segunda, reinício depois da
/// última semana definida e limites do intervalo do plano.
/// </summary>
public sealed class TrainingPlanScheduleTests
{
    // 2026-09-02 é uma quarta-feira; a segunda dessa semana é 2026-08-31.
    private static readonly DateOnly Wednesday = new(2026, 9, 2);

    [Theory]
    [InlineData(2026, 9, 2, 2)]  // quarta
    [InlineData(2026, 9, 6, 6)]  // domingo
    [InlineData(2026, 9, 7, 0)]  // segunda
    public void ToPlanDayOfWeek_MapsMondayToZero(int year, int month, int day, int expected) =>
        Assert.Equal(expected, TrainingPlanSchedule.ToPlanDayOfWeek(new DateOnly(year, month, day)));

    [Fact]
    public void Resolve_BeforeStart_ReturnsNull() =>
        Assert.Null(TrainingPlanSchedule.Resolve(Wednesday, null, 4, Wednesday.AddDays(-1)));

    [Fact]
    public void Resolve_AfterEnd_ReturnsNull() =>
        Assert.Null(TrainingPlanSchedule.Resolve(
            Wednesday, Wednesday.AddDays(10), 4, Wednesday.AddDays(11)));

    [Fact]
    public void Resolve_OnStartDay_IsFirstWeek()
    {
        var slot = TrainingPlanSchedule.Resolve(Wednesday, null, 4, Wednesday);

        Assert.Equal(1, slot!.Value.WeekNumber);
        Assert.Equal(2, slot.Value.DayOfWeek);
    }

    [Fact]
    public void Resolve_SundayOfStartWeek_StaysInFirstWeek()
    {
        // O plano começa a uma quarta: o domingo seguinte ainda é a semana 1.
        var slot = TrainingPlanSchedule.Resolve(Wednesday, null, 4, Wednesday.AddDays(4));

        Assert.Equal(1, slot!.Value.WeekNumber);
        Assert.Equal(6, slot.Value.DayOfWeek);
    }

    [Fact]
    public void Resolve_NextMonday_StartsSecondWeek()
    {
        var slot = TrainingPlanSchedule.Resolve(Wednesday, null, 4, Wednesday.AddDays(5));

        Assert.Equal(2, slot!.Value.WeekNumber);
        Assert.Equal(0, slot.Value.DayOfWeek);
    }

    [Fact]
    public void Resolve_AfterLastWeek_RestartsCycle()
    {
        // Ciclo de 4 semanas: a quinta semana volta à semana 1.
        var slot = TrainingPlanSchedule.Resolve(Wednesday, null, 4, Wednesday.AddDays(5 + 21));

        Assert.Equal(1, slot!.Value.WeekNumber);
    }

    [Fact]
    public void Resolve_SingleWeekPlan_RepeatsEveryWeek()
    {
        var slot = TrainingPlanSchedule.Resolve(Wednesday, null, 1, Wednesday.AddDays(35));

        Assert.Equal(1, slot!.Value.WeekNumber);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(53)]
    public void Resolve_WithInvalidCycle_Throws(int cycleLengthWeeks) =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TrainingPlanSchedule.Resolve(Wednesday, null, cycleLengthWeeks, Wednesday));

    [Fact]
    public void FindNextScheduledDate_ReturnsNextMatchingDay()
    {
        var slots = new HashSet<ScheduledSlot> { new(1, 4) }; // sexta da semana 1

        var next = TrainingPlanSchedule.FindNextScheduledDate(Wednesday, null, 1, Wednesday, slots);

        Assert.Equal(new DateOnly(2026, 9, 4), next);
    }

    [Fact]
    public void FindNextScheduledDate_BeforePlanStart_StartsAtPlanStart()
    {
        var slots = new HashSet<ScheduledSlot> { new(1, 2) }; // a própria quarta de início

        var next = TrainingPlanSchedule.FindNextScheduledDate(
            Wednesday, null, 1, Wednesday.AddDays(-5), slots);

        Assert.Equal(Wednesday, next);
    }

    [Fact]
    public void FindNextScheduledDate_WhenPlanEndsFirst_ReturnsNull()
    {
        var slots = new HashSet<ScheduledSlot> { new(1, 0) }; // segunda

        var next = TrainingPlanSchedule.FindNextScheduledDate(
            Wednesday, Wednesday.AddDays(2), 1, Wednesday, slots);

        Assert.Null(next);
    }

    [Fact]
    public void FindNextScheduledDate_WithoutSlots_ReturnsNull() =>
        Assert.Null(TrainingPlanSchedule.FindNextScheduledDate(
            Wednesday, null, 1, Wednesday, new HashSet<ScheduledSlot>()));
}
