using Application.Features.Supplements.Abstractions;
using Domain.Entities.Clients;
using Domain.Entities.Identity;
using Application.Features.Training.ExerciseSetLogs.Abstractions;
using Application.Features.Training.WorkoutCompletions.Abstractions;
using Domain.Entities.Supplements;
using Domain.Entities.Training;
using Domain.ValueObjects;
using Infrastructure.Data;
using Infrastructure.IntegrationTests.Support;
using Infrastructure.Persistence.Supplements;
using Infrastructure.Persistence.Training;
using Infrastructure.Time;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.IntegrationTests.ClientPortal;

/// <summary>
/// Prova em PostgreSQL 17 as escritas do portal da Sprint 6A: titularidade do cliente,
/// janela de hoje, bloqueio de desmarcar depois de concluir, idempotência e histórico.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class Sprint6AClientWriteStoresTests
{
    // 12:00 UTC é o mesmo dia civil em Europe/Lisbon (fuso por omissão do trainer).
    private static readonly DateTime Now = new(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = new(2026, 9, 16);

    private readonly PostgresContainerFixture _fixture;

    public Sprint6AClientWriteStoresTests(PostgresContainerFixture fixture) => _fixture = fixture;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RecordSet_OwnActivePlan_PersistsLogWithRpeAndServerInstant()
    {
        var seed = await SeedAsync();
        await using var context = _fixture.CreateContext(seed.TrainerId);

        var result = await SetLogStore(context).RecordAsync(
            seed.TrainerId, seed.ClientUserId, seed.DayExerciseId, 1, 60m, 8, 8.5m, "ok", Now, Token);

        Assert.Equal(MyExerciseSetLogStoreResult.Status.Recorded, result.Kind);
        var stored = await context.ClientExerciseSetLogs.AsNoTracking().SingleAsync(Token);
        Assert.Equal((seed.ClientId, 8.5m, new DateTimeOffset(Now)), (stored.ClientId, stored.Rpe, stored.PerformedAt));
    }

    [Fact]
    public async Task RecordSet_PrescriptionOfAnotherClientOfSameTrainer_ReturnsNotFound()
    {
        var seed = await SeedAsync();
        var intruderUserId = await SeedSecondClientAsync(seed.TrainerId);
        await using var context = _fixture.CreateContext(seed.TrainerId);

        var result = await SetLogStore(context).RecordAsync(
            seed.TrainerId, intruderUserId, seed.DayExerciseId, 1, 60m, 8, null, null, Now, Token);

        Assert.Equal(MyExerciseSetLogStoreResult.Status.NotFound, result.Kind);
        Assert.Equal(0, await context.ClientExerciseSetLogs.CountAsync(Token));
    }

    [Fact]
    public async Task RecordSet_ArchivedPlan_ReturnsInactive()
    {
        var seed = await SeedAsync();
        await ArchivePlanAsync(seed);
        await using var context = _fixture.CreateContext(seed.TrainerId);

        var result = await SetLogStore(context).RecordAsync(
            seed.TrainerId, seed.ClientUserId, seed.DayExerciseId, 1, 60m, 8, null, null, Now, Token);

        Assert.Equal(MyExerciseSetLogStoreResult.Status.TrainingPlanInactive, result.Kind);
    }

    [Fact]
    public async Task RecordSet_BeforePlanStart_ReturnsDateOutsidePlan()
    {
        var seed = await SeedAsync(startDate: Today.AddDays(1));
        await using var context = _fixture.CreateContext(seed.TrainerId);

        var result = await SetLogStore(context).RecordAsync(
            seed.TrainerId, seed.ClientUserId, seed.DayExerciseId, 1, 60m, 8, null, null, Now, Token);

        Assert.Equal(MyExerciseSetLogStoreResult.Status.DateOutsidePlan, result.Kind);
    }

    [Fact]
    public async Task RecordSet_UnknownSetNumber_ReturnsSetNotFound()
    {
        var seed = await SeedAsync();
        await using var context = _fixture.CreateContext(seed.TrainerId);

        var result = await SetLogStore(context).RecordAsync(
            seed.TrainerId, seed.ClientUserId, seed.DayExerciseId, 9, 60m, 8, null, null, Now, Token);

        Assert.Equal(MyExerciseSetLogStoreResult.Status.SetNotFound, result.Kind);
    }

    [Fact]
    public async Task CorrectSet_LogFromPreviousDay_ReturnsNotEditable()
    {
        var seed = await SeedAsync();
        var logId = await RecordAsync(seed, Now.AddDays(-1));
        await using var context = _fixture.CreateContext(seed.TrainerId);

        var result = await SetLogStore(context).CorrectAsync(
            seed.TrainerId, seed.ClientUserId, logId, 70m, 6, 9m, null, Now, Token);

        Assert.Equal(MyExerciseSetLogStoreResult.Status.NotEditable, result.Kind);
    }

    [Fact]
    public async Task CorrectSet_TodayLog_KeepsPerformedAtAndUpdatesValues()
    {
        var seed = await SeedAsync();
        var logId = await RecordAsync(seed, Now.AddMinutes(-30));
        await using var context = _fixture.CreateContext(seed.TrainerId);

        var result = await SetLogStore(context).CorrectAsync(
            seed.TrainerId, seed.ClientUserId, logId, 70m, 6, 9m, null, Now, Token);

        Assert.Equal(MyExerciseSetLogStoreResult.Status.Corrected, result.Kind);
        Assert.Equal((70m, 9m, new DateTimeOffset(Now.AddMinutes(-30))),
            (result.Log!.WeightKg, result.Log.Rpe, result.Log.PerformedAt));
    }

    /// <summary>
    /// QG6A-TEST-004: a janela de hoje segue o fuso do trainer (Europe/Lisbon, UTC+1 em
    /// setembro), não o UTC. 23:30Z do dia 15 já é dia 16 em Lisboa; 22:30Z ainda é dia 15.
    /// </summary>
    [Fact]
    public async Task EditSet_AtLocalMidnightBoundary_UsesTrainerTimeZone()
    {
        var seed = await SeedAsync();
        var afterLocalMidnight = await RecordAsync(seed, new DateTime(2026, 9, 15, 23, 30, 0, DateTimeKind.Utc));
        var beforeLocalMidnight = await RecordAsync(seed, new DateTime(2026, 9, 15, 22, 30, 0, DateTimeKind.Utc));
        await using var context = _fixture.CreateContext(seed.TrainerId);
        var store = SetLogStore(context);

        var today = await store.CorrectAsync(
            seed.TrainerId, seed.ClientUserId, afterLocalMidnight, 70m, 6, null, null, Now, Token);
        var yesterday = await store.CorrectAsync(
            seed.TrainerId, seed.ClientUserId, beforeLocalMidnight, 70m, 6, null, null, Now, Token);

        Assert.Equal(
            (MyExerciseSetLogStoreResult.Status.Corrected, MyExerciseSetLogStoreResult.Status.NotEditable),
            (today.Kind, yesterday.Kind));
    }

    [Fact]
    public async Task DeleteSet_AnotherClientsLog_ReturnsNotFoundAndKeepsRow()
    {
        var seed = await SeedAsync();
        var logId = await RecordAsync(seed, Now.AddMinutes(-5));
        var intruderUserId = await SeedSecondClientAsync(seed.TrainerId);
        await using var context = _fixture.CreateContext(seed.TrainerId);

        var result = await SetLogStore(context).DeleteAsync(
            seed.TrainerId, intruderUserId, logId, Now, Token);

        Assert.Equal(MyExerciseSetLogStoreResult.Status.NotFound, result.Kind);
        Assert.Equal(1, await context.ClientExerciseSetLogs.CountAsync(Token));
    }

    [Fact]
    public async Task DeleteSet_AfterWorkoutCompleted_IsBlocked()
    {
        var seed = await SeedAsync();
        var logId = await RecordAsync(seed, Now.AddMinutes(-5));
        await using (var completionContext = _fixture.CreateContext(seed.TrainerId))
        {
            var completed = await CompletionStore(completionContext).CompleteAsync(
                seed.TrainerId, seed.ClientUserId, seed.DayId, null, Now, Token);
            Assert.Equal(WorkoutCompletionStoreResult.Status.Completed, completed.Kind);
        }

        await using var context = _fixture.CreateContext(seed.TrainerId);
        var result = await SetLogStore(context).DeleteAsync(
            seed.TrainerId, seed.ClientUserId, logId, Now, Token);

        Assert.Equal(MyExerciseSetLogStoreResult.Status.WorkoutAlreadyCompleted, result.Kind);
        Assert.Equal(1, await context.ClientExerciseSetLogs.CountAsync(Token));
    }

    [Fact]
    public async Task DeleteSet_TodayBeforeCompletion_RemovesLog()
    {
        var seed = await SeedAsync();
        var logId = await RecordAsync(seed, Now.AddMinutes(-5));
        await using var context = _fixture.CreateContext(seed.TrainerId);

        var result = await SetLogStore(context).DeleteAsync(
            seed.TrainerId, seed.ClientUserId, logId, Now, Token);

        Assert.Equal(MyExerciseSetLogStoreResult.Status.Deleted, result.Kind);
        Assert.Equal(0, await context.ClientExerciseSetLogs.CountAsync(Token));
    }

    [Fact]
    public async Task CompleteWorkout_Twice_ReturnsSameCompletionAndSingleRow()
    {
        var seed = await SeedAsync();
        await using var context = _fixture.CreateContext(seed.TrainerId);
        var store = CompletionStore(context);

        var first = await store.CompleteAsync(seed.TrainerId, seed.ClientUserId, seed.DayId, "partial", Now, Token);
        var second = await store.CompleteAsync(seed.TrainerId, seed.ClientUserId, seed.DayId, null, Now.AddMinutes(5), Token);

        Assert.Equal(WorkoutCompletionStoreResult.Status.Completed, first.Kind);
        Assert.Equal(WorkoutCompletionStoreResult.Status.AlreadyCompleted, second.Kind);
        Assert.Equal(first.Completion!.Id, second.Completion!.Id);
        Assert.Equal((Today, seed.PlanId), (first.Completion.LocalDate, first.Completion.TrainingPlanId));
        Assert.Equal(1, await context.WorkoutCompletions.CountAsync(Token));
    }

    /// <summary>
    /// QG6A-TEST-004: dois pedidos simultâneos no mesmo dia serializam pelo lock do plano;
    /// um escreve, o outro devolve a mesma conclusão, sem violação do unique.
    /// </summary>
    [Fact]
    public async Task CompleteWorkout_Concurrent_IsSerializedOrIdempotent()
    {
        var seed = await SeedAsync();
        await using var firstContext = _fixture.CreateContext(seed.TrainerId);
        await using var secondContext = _fixture.CreateContext(seed.TrainerId);

        var results = await Task.WhenAll(
            CompletionStore(firstContext).CompleteAsync(
                seed.TrainerId, seed.ClientUserId, seed.DayId, null, Now, Token),
            CompletionStore(secondContext).CompleteAsync(
                seed.TrainerId, seed.ClientUserId, seed.DayId, null, Now, Token));

        Assert.Single(results, result => result.Kind == WorkoutCompletionStoreResult.Status.Completed);
        Assert.Single(results, result => result.Kind == WorkoutCompletionStoreResult.Status.AlreadyCompleted);
        Assert.Equal(results[0].Completion!.Id, results[1].Completion!.Id);
        Assert.Equal(1, await firstContext.WorkoutCompletions.CountAsync(Token));
    }

    [Fact]
    public async Task CompleteWorkout_DayOfAnotherClient_ReturnsNotFound()
    {
        var seed = await SeedAsync();
        var intruderUserId = await SeedSecondClientAsync(seed.TrainerId);
        await using var context = _fixture.CreateContext(seed.TrainerId);

        var result = await CompletionStore(context).CompleteAsync(
            seed.TrainerId, intruderUserId, seed.DayId, null, Now, Token);

        Assert.Equal(WorkoutCompletionStoreResult.Status.NotFound, result.Kind);
    }

    [Fact]
    public async Task CompletionWithoutSets_FreezesStructureAsHistory()
    {
        var seed = await SeedAsync();
        await using (var completionContext = _fixture.CreateContext(seed.TrainerId))
        {
            await CompletionStore(completionContext).CompleteAsync(
                seed.TrainerId, seed.ClientUserId, seed.DayId, null, Now, Token);
        }

        // O dia referenciado por uma conclusão não pode desaparecer (FK Restrict).
        await using var context = _fixture.CreateContext(seed.TrainerId);
        var day = await context.TrainingPlanDays.SingleAsync(item => item.Id == seed.DayId, Token);
        context.TrainingPlanDays.Remove(day);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(Token));
    }

    [Fact]
    public async Task MarkIntake_TwiceThenUnmarkTwice_IsIdempotent()
    {
        var seed = await SeedAsync();
        await using var context = _fixture.CreateContext(seed.TrainerId);
        var store = IntakeStore(context);

        var first = await store.MarkAsync(seed.TrainerId, seed.ClientUserId, seed.AssignmentId, Now, Token);
        var second = await store.MarkAsync(seed.TrainerId, seed.ClientUserId, seed.AssignmentId, Now.AddMinutes(1), Token);
        var unmarked = await store.UnmarkAsync(seed.TrainerId, seed.ClientUserId, seed.AssignmentId, Now, Token);
        var again = await store.UnmarkAsync(seed.TrainerId, seed.ClientUserId, seed.AssignmentId, Now, Token);

        Assert.Equal(
            (SupplementIntakeStoreResult.Status.Marked, SupplementIntakeStoreResult.Status.AlreadyMarked,
                SupplementIntakeStoreResult.Status.Unmarked, SupplementIntakeStoreResult.Status.NotMarked),
            (first.Kind, second.Kind, unmarked.Kind, again.Kind));
        Assert.Equal(Now, second.Intake!.TakenAt);
        Assert.Equal(0, await context.ClientSupplementIntakes.CountAsync(Token));
    }

    /// <summary>
    /// QG6A-TEST-004: duas marcações simultâneas da mesma toma serializam pelo lock da
    /// atribuição; uma escreve, a outra devolve a mesma toma, sem violação do unique.
    /// </summary>
    [Fact]
    public async Task MarkIntake_Concurrent_IsSerializedOrIdempotent()
    {
        var seed = await SeedAsync();
        await using var firstContext = _fixture.CreateContext(seed.TrainerId);
        await using var secondContext = _fixture.CreateContext(seed.TrainerId);

        var results = await Task.WhenAll(
            IntakeStore(firstContext).MarkAsync(
                seed.TrainerId, seed.ClientUserId, seed.AssignmentId, Now, Token),
            IntakeStore(secondContext).MarkAsync(
                seed.TrainerId, seed.ClientUserId, seed.AssignmentId, Now, Token));

        Assert.Single(results, result => result.Kind == SupplementIntakeStoreResult.Status.Marked);
        Assert.Single(results, result => result.Kind == SupplementIntakeStoreResult.Status.AlreadyMarked);
        Assert.Equal(results[0].Intake!.Id, results[1].Intake!.Id);
        Assert.Equal(1, await firstContext.ClientSupplementIntakes.CountAsync(Token));
    }

    [Fact]
    public async Task MarkIntake_InactiveAssignment_ReturnsInactive()
    {
        var seed = await SeedAsync();
        await using (var trainerContext = _fixture.CreateContext(seed.TrainerId))
        {
            var assignment = await trainerContext.ClientSupplementAssignments
                .SingleAsync(item => item.Id == seed.AssignmentId, Token);
            assignment.Deactivate(Now);
            await trainerContext.SaveChangesAsync(Token);
        }

        await using var context = _fixture.CreateContext(seed.TrainerId);
        var result = await IntakeStore(context).MarkAsync(
            seed.TrainerId, seed.ClientUserId, seed.AssignmentId, Now, Token);

        Assert.Equal(SupplementIntakeStoreResult.Status.AssignmentInactive, result.Kind);
    }

    [Fact]
    public async Task MarkIntake_AssignmentOfAnotherClient_ReturnsNotFound()
    {
        var seed = await SeedAsync();
        var intruderUserId = await SeedSecondClientAsync(seed.TrainerId);
        await using var context = _fixture.CreateContext(seed.TrainerId);

        var result = await IntakeStore(context).MarkAsync(
            seed.TrainerId, intruderUserId, seed.AssignmentId, Now, Token);

        Assert.Equal(SupplementIntakeStoreResult.Status.AssignmentNotFound, result.Kind);
        Assert.Equal(0, await context.ClientSupplementIntakes.CountAsync(Token));
    }

    [Fact]
    public async Task ListTodayIntakes_ReturnsTakenAndTotalCounts()
    {
        var seed = await SeedAsync();
        await using (var markContext = _fixture.CreateContext(seed.TrainerId))
        {
            await IntakeStore(markContext).MarkAsync(
                seed.TrainerId, seed.ClientUserId, seed.AssignmentId, Now, Token);
        }

        await using var context = _fixture.CreateContext(seed.TrainerId);
        var today = await new SupplementIntakeQueries(context).ListMyForDateAsync(
            seed.TrainerId, seed.ClientUserId, Today, Token);
        var tomorrow = await new SupplementIntakeQueries(context).ListMyForDateAsync(
            seed.TrainerId, seed.ClientUserId, Today.AddDays(1), Token);

        Assert.Equal((1, 1, true), (today!.TakenCount, today.TotalCount, today.Items.Single().IsTaken));
        Assert.Equal((0, 1), (tomorrow!.TakenCount, tomorrow.TotalCount));
    }

    private static MyExerciseSetLogStore SetLogStore(PtManagerDbContext context) =>
        new(context, new TrainerTimeZoneProvider(context));

    private static WorkoutCompletionStore CompletionStore(PtManagerDbContext context) =>
        new(context, new TrainerTimeZoneProvider(context));

    private static SupplementIntakeStore IntakeStore(PtManagerDbContext context) =>
        new(context, new TrainerTimeZoneProvider(context));

    private async Task<Guid> RecordAsync(Seed seed, DateTime instant)
    {
        await using var context = _fixture.CreateContext(seed.TrainerId);
        var result = await SetLogStore(context).RecordAsync(
            seed.TrainerId, seed.ClientUserId, seed.DayExerciseId, 1, 60m, 8, null, null, instant, Token);
        Assert.Equal(MyExerciseSetLogStoreResult.Status.Recorded, result.Kind);
        return result.Log!.Id;
    }

    private async Task ArchivePlanAsync(Seed seed)
    {
        await using var context = _fixture.CreateContext(seed.TrainerId);
        var plan = await context.TrainingPlans.SingleAsync(item => item.Id == seed.PlanId, Token);
        plan.Archive(Now);
        await context.SaveChangesAsync(Token);
    }

    private async Task<Guid> SeedSecondClientAsync(Guid trainerId)
    {
        // Segundo cliente do mesmo trainer: mesmo tenant, utilizador e cliente diferentes.
        await using var context = _fixture.CreateContext(trainerId);
        var user = new User(new EmailAddress($"s6a-second-{Guid.NewGuid():N}@example.test"), "client", "Second", Now);
        user.SetPasswordHash("integration-test-password-hash", Now);
        var client = new Client(
            trainerId,
            "Second client",
            user.Email,
            "+351911111111",
            BirthDate.Create(new DateOnly(1990, 1, 1), Today),
            BiologicalSex.Female,
            null, null, null, null,
            Now);
        client.AttachUser(user.Id, Now);
        context.Users.Add(user);
        context.Clients.Add(client);
        await context.SaveChangesAsync(Token);
        return user.Id;
    }

    private async Task<Seed> SeedAsync(DateOnly? startDate = null)
    {
        var tenant = await _fixture.SeedTenantWithClientAsync($"s6a-{Guid.NewGuid():N}", Token);
        await using var context = _fixture.CreateContext(tenant.TrainerId);

        var exercise = new Exercise(tenant.TrainerId, "Squat", null, "quadriceps", null, null, null, Now);
        var plan = new TrainingPlan(
            tenant.TrainerId, tenant.ClientId, "Plan", null, null, null,
            startDate ?? new DateOnly(2026, 9, 1), null, Now);
        var day = plan.AddDay(2, 1, null, Now);
        var prescription = day.AddExercise(exercise.Id, 1, null, null, null, Now);
        prescription.AddSet(1, 8, 60m, 60, 90, Now, plannedRpe: 8m);

        var supplement = new Supplement(tenant.TrainerId, tenant.TrainerId, "Creatine", null, "g", "5 g", "Daily", null, Now);
        var assignment = new ClientSupplementAssignment(
            tenant.TrainerId, tenant.ClientId, supplement.Id, "5 g", "After workout", null, Now);

        context.Exercises.Add(exercise);
        context.TrainingPlans.Add(plan);
        context.Supplements.Add(supplement);
        context.ClientSupplementAssignments.Add(assignment);
        await context.SaveChangesAsync(Token);

        return new Seed(
            tenant.TrainerId, tenant.ClientId, tenant.ClientUserId,
            plan.Id, day.Id, prescription.Id, assignment.Id);
    }

    private sealed record Seed(
        Guid TrainerId,
        Guid ClientId,
        Guid ClientUserId,
        Guid PlanId,
        Guid DayId,
        Guid DayExerciseId,
        Guid AssignmentId);
}
