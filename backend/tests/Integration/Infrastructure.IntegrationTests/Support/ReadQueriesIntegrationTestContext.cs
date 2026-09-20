using System.Data.Common;
using Application.Features.Dashboard;
using Application.Features.Dashboard.Abstractions;
using Application.Features.Dashboard.Dtos;
using Domain.Entities.Assessments;
using Domain.Entities.Nutrition;
using Domain.Services;
using Domain.ValueObjects;
using Infrastructure.Data;
using Infrastructure.Data.Interceptors;
using Infrastructure.Persistence.Dashboard;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Infrastructure.IntegrationTests.Support;

public abstract class ReadQueriesIntegrationTestContext
{
    protected static readonly DateTime Now = new(2026, 9, 3, 12, 0, 0, DateTimeKind.Utc);
    protected static readonly DateOnly LocalToday = new(2026, 9, 3);
    protected readonly PostgresContainerFixture _fixture;

    protected ReadQueriesIntegrationTestContext(PostgresContainerFixture fixture) =>
        _fixture = fixture;

    protected static string NewDiscriminator() => Guid.NewGuid().ToString("N")[..12];

    protected static TrainerDashboardWindow Window() =>
        new(
            LocalToday,
            new DateTimeOffset(2026, 9, 2, 23, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 9, 3, 23, 0, 0, TimeSpan.Zero),
            Now,
            Now.AddHours(-DashboardThresholds.ReviewOverdueAfterHours),
            LocalToday.AddDays(DashboardThresholds.PackEndingWithinDays),
            LocalToday.AddDays(DashboardThresholds.PlanExpiringWithinDays),
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 10, 1));

    protected static MealPlan MealPlanFor(PostgresContainerFixture.TestTenantSeed seed, DateOnly? endsDate)
    {
        var macros = MacroTargetCalculator.CalculateFromManualGrams(
            2_000m, new ManualMacroInput(150m, 200m, 66.67m));
        var snapshot = NutritionCalculationSnapshot.FromManualEnergy(80m, macros, Now);
        return new MealPlan(
            seed.TrainerId,
            seed.ClientId,
            $"Plano {Guid.NewGuid():N}"[..20],
            null,
            new DateOnly(2026, 9, 1),
            endsDate,
            snapshot,
            Now);
    }

    protected async Task<Application.Features.Dashboard.Dtos.TrainerDashboardData> ReadDashboardAsync(
        Guid trainerId,
        CancellationToken cancellationToken)
    {
        await using var context = _fixture.CreateContext(trainerId);
        return await new TrainerDashboardQueries(context)
            .GetAsync(trainerId, Window(), cancellationToken);
    }

    protected async Task SeedPendingReviewCheckInsAsync(
        PostgresContainerFixture.TestTenantSeed seed,
        int count,
        CancellationToken cancellationToken)
    {
        await using var context = _fixture.CreateContext(seed.TrainerId);

        for (var index = 0; index < count; index++)
        {
            // Datas distintas respeitam o índice único (trainer, cliente, data).
            var checkInDate = LocalToday.AddDays(-(index + 3));
            var checkIn = new CheckIn(seed.TrainerId, seed.ClientId, checkInDate, null, Now);
            checkIn.SubmitResponse(
                70m + index,
                null,
                null,
                null,
                null,
                null,
                null,
                checkInDate,
                Now.AddDays(-(index + 3)));
            context.CheckIns.Add(checkIn);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    protected async Task<Guid> SeedPrivateFoodAsync(
        Guid trainerId,
        string name,
        CancellationToken cancellationToken)
    {
        await using var context = _fixture.CreateContext(trainerId);
        var food = new Food(trainerId, name, null, 2.7m, 28m, 0.3m, null, Now);
        context.Foods.Add(food);
        await context.SaveChangesAsync(cancellationToken);
        return food.Id;
    }

    protected async Task SeedGlobalFoodAsync(string name, CancellationToken cancellationToken)
    {
        // O catálogo global só se semeia por SQL direto: o interceptor exige auditoria
        // administrativa para qualquer escrita global feita pelo DbContext.
        await _fixture.ExecuteSqlAsync(
            """
            INSERT INTO foods (
                id, owner_trainer_id, name, description, protein, carbs, fats, fiber,
                is_active, platform_enforcement_status, created_at, updated_at)
            VALUES (
                gen_random_uuid(), NULL, @name, NULL, 2.7, 28, 0.3, NULL,
                true, 'allowed', now(), now())
            """,
            cancellationToken,
            new Npgsql.NpgsqlParameter("name", name));
    }

    protected PtManagerDbContext CreateMeasuredContext(Guid trainerId, CommandCounter counter)
    {
        var tenantContext = TestTenantContext.ForTrainer(trainerId);
        var options = new DbContextOptionsBuilder<PtManagerDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .AddInterceptors(new TenantWriteValidationInterceptor(tenantContext), counter)
            .Options;
        return new PtManagerDbContext(options, tenantContext);
    }

    protected sealed class CommandCounter : DbCommandInterceptor
    {
        public int ReaderCommands { get; private set; }

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            ReaderCommands++;
            return result;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            ReaderCommands++;
            return ValueTask.FromResult(result);
        }
    }
}
