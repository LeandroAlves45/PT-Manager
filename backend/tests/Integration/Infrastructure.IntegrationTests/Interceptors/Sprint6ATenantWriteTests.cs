using Domain.Entities.Supplements;
using Domain.Entities.Training;
using Domain.Exceptions;
using Infrastructure.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.IntegrationTests.Interceptors;

/// <summary>
/// Prova que o interceptor recusa conclusões e tomas que apontam para outro tenant ou
/// outro cliente, e que a eliminação de uma toma passa pela validação de owner.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class Sprint6ATenantWriteTests
{
    private static readonly DateTime Now = new(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = new(2026, 9, 16);

    private readonly PostgresContainerFixture _fixture;

    public Sprint6ATenantWriteTests(PostgresContainerFixture fixture) => _fixture = fixture;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Completion_ForDayOfAnotherTenant_IsRejected()
    {
        var owner = await SeedPlanAsync();
        var intruder = await _fixture.SeedTenantWithClientAsync($"s6a-int-{Guid.NewGuid():N}", Token);

        await using var context = _fixture.CreateContext(intruder.TrainerId);
        context.WorkoutCompletions.Add(new WorkoutCompletion(
            intruder.TrainerId, intruder.ClientId, owner.PlanId, owner.DayId, Today, null, Now));

        await Assert.ThrowsAsync<DomainException>(() => context.SaveChangesAsync(Token));
    }

    [Fact]
    public async Task Completion_WithClientDifferentFromPlanClient_IsRejected()
    {
        var owner = await SeedPlanAsync();

        await using var context = _fixture.CreateContext(owner.TrainerId);
        context.WorkoutCompletions.Add(new WorkoutCompletion(
            owner.TrainerId, Guid.NewGuid(), owner.PlanId, owner.DayId, Today, null, Now));

        await Assert.ThrowsAsync<DomainException>(() => context.SaveChangesAsync(Token));
    }

    [Fact]
    public async Task Intake_ForAssignmentOfAnotherTenant_IsRejected()
    {
        var owner = await SeedPlanAsync();
        var intruder = await _fixture.SeedTenantWithClientAsync($"s6a-int-{Guid.NewGuid():N}", Token);

        await using var context = _fixture.CreateContext(intruder.TrainerId);
        context.ClientSupplementIntakes.Add(new ClientSupplementIntake(
            intruder.TrainerId, intruder.ClientId, owner.AssignmentId, Today, Now));

        await Assert.ThrowsAsync<DomainException>(() => context.SaveChangesAsync(Token));
    }

    [Fact]
    public async Task IntakeDelete_FromAnotherTenantContext_IsRejected()
    {
        var owner = await SeedPlanAsync();
        Guid intakeId;
        await using (var ownerContext = _fixture.CreateContext(owner.TrainerId))
        {
            var intake = new ClientSupplementIntake(owner.TrainerId, owner.ClientId, owner.AssignmentId, Today, Now);
            ownerContext.ClientSupplementIntakes.Add(intake);
            await ownerContext.SaveChangesAsync(Token);
            intakeId = intake.Id;
        }

        var intruder = await _fixture.SeedTenantWithClientAsync($"s6a-int-{Guid.NewGuid():N}", Token);
        await using var context = _fixture.CreateContext(intruder.TrainerId);
        var loaded = await context.ClientSupplementIntakes
            .IgnoreQueryFilters()
            .SingleAsync(item => item.Id == intakeId, Token);
        context.ClientSupplementIntakes.Remove(loaded);

        await Assert.ThrowsAsync<DomainException>(() => context.SaveChangesAsync(Token));
    }

    [Fact]
    public async Task QueryFilters_HideCompletionsAndIntakesFromAnotherTenant()
    {
        var owner = await SeedPlanAsync();
        await using (var ownerContext = _fixture.CreateContext(owner.TrainerId))
        {
            ownerContext.WorkoutCompletions.Add(new WorkoutCompletion(
                owner.TrainerId, owner.ClientId, owner.PlanId, owner.DayId, Today, null, Now));
            ownerContext.ClientSupplementIntakes.Add(new ClientSupplementIntake(
                owner.TrainerId, owner.ClientId, owner.AssignmentId, Today, Now));
            await ownerContext.SaveChangesAsync(Token);
        }

        var intruder = await _fixture.SeedTenantWithClientAsync($"s6a-int-{Guid.NewGuid():N}", Token);
        await using var context = _fixture.CreateContext(intruder.TrainerId);

        Assert.False(await context.WorkoutCompletions.AnyAsync(item => item.ClientId == owner.ClientId, Token));
        Assert.False(await context.ClientSupplementIntakes.AnyAsync(item => item.ClientId == owner.ClientId, Token));
    }

    private async Task<PlanSeed> SeedPlanAsync()
    {
        var tenant = await _fixture.SeedTenantWithClientAsync($"s6a-own-{Guid.NewGuid():N}", Token);
        await using var context = _fixture.CreateContext(tenant.TrainerId);
        var exercise = new Exercise(tenant.TrainerId, "Row", null, "back", null, null, null, Now);
        var plan = new TrainingPlan(tenant.TrainerId, tenant.ClientId, "Plan", null, null, null,
            new DateOnly(2026, 9, 1), null, Now);
        var day = plan.AddDay(2, 1, null, Now);
        day.AddExercise(exercise.Id, 1, null, null, null, Now).AddSet(1, 8, 50m, 60, 90, Now);
        var supplement = new Supplement(tenant.TrainerId, tenant.TrainerId, "Omega 3", null, "caps", "1", "Lunch", null, Now);
        var assignment = new ClientSupplementAssignment(
            tenant.TrainerId, tenant.ClientId, supplement.Id, "1", "Lunch", null, Now);
        context.Exercises.Add(exercise);
        context.TrainingPlans.Add(plan);
        context.Supplements.Add(supplement);
        context.ClientSupplementAssignments.Add(assignment);
        await context.SaveChangesAsync(Token);
        return new PlanSeed(tenant.TrainerId, tenant.ClientId, plan.Id, day.Id, assignment.Id);
    }

    private sealed record PlanSeed(Guid TrainerId, Guid ClientId, Guid PlanId, Guid DayId, Guid AssignmentId);
}
