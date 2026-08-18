using Domain.Entities.Assessments;
using Domain.Entities.Nutrition;
using Domain.Services;
using Domain.ValueObjects;
using Infrastructure.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Infrastructure.IntegrationTests.Data;

[Collection(PostgresCollection.Name)]
public sealed class RoundTripAndConstraintTests
{
    private static readonly DateTime Now = new(2026, 8, 5, 13, 0, 0, DateTimeKind.Utc);

    private readonly PostgresContainerFixture _fixture;

    public RoundTripAndConstraintTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Client_WhenReloaded_PreservesBirthDateAndBiologicalSex()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(
            Guid.NewGuid().ToString("N"), cancellationToken);
        await using var context = _fixture.CreateContext(tenant.TrainerId);

        // Act
        var client = await context.Clients
            .AsNoTracking()
            .SingleAsync(value => value.Id == tenant.ClientId, cancellationToken);

        // Assert
        Assert.Equal(
            (new DateOnly(1995, 1, 1), BiologicalSex.Male.Value),
            (client.BirthDate.Value, client.Sex.Value));
    }

    [Fact]
    public async Task InitialAssessment_WhenReloaded_PreservesValueObjectsAndJson()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(
            Guid.NewGuid().ToString("N"), cancellationToken);
        var measurements = new BodyMeasurements(
            81.2m, 98.4m, 102.5m, 35.1m, 34.9m, 60.2m, 59.8m, 39.5m, 39.2m);
        var nutrition = new NutritionIntake(
            "Mediterranean",
            "None",
            null,
            null,
            null,
            "Three meals",
            4,
            5,
            2,
            2.5m,
            "Evening",
            true,
            "Creatine",
            "Integration round-trip");
        var assessment = new InitialAssessment(
            tenant.TrainerId,
            tenant.ClientId,
            80m,
            180,
            15m,
            null,
            "intermediate",
            ActivityLevel.VeryActive,
            "Strength",
            "Developer",
            measurements,
            nutrition,
            Now);

        await using var context = _fixture.CreateContext(tenant.TrainerId);
        context.InitialAssessments.Add(assessment);
        await context.SaveChangesAsync(cancellationToken);
        context.ChangeTracker.Clear();

        // Act
        var stored = await context.InitialAssessments
            .AsNoTracking()
            .SingleAsync(value => value.Id == assessment.Id, cancellationToken);

        // Assert
        Assert.Equal(
            (ActivityLevel.VeryActive.Value,
                (decimal?)81.2m,
                (string?)"Mediterranean",
                (decimal?)2.5m),
            (stored.ActivityLevel.Value,
                stored.BodyMeasurements.WaistCm,
                stored.NutritionIntake.FoodPreferences,
                stored.NutritionIntake.AvgWaterLitersPerDay));
    }

    [Fact]
    public async Task CheckIn_WhenReloaded_PreservesBodyMeasurementsAndFeedback()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(
            Guid.NewGuid().ToString("N"), cancellationToken);
        var checkIn = new CheckIn(
            tenant.TrainerId,
            tenant.ClientId,
            new DateOnly(2026, 8, 5),
            null,
            Now);
        checkIn.SubmitResponse(
            79.4m,
            14.5m,
            null,
            new BodyMeasurements(
                80.1m, 97.2m, 101.3m, 34.4m, 34.2m, 59.5m, 59.3m, 38.8m, 38.6m),
            new CheckInFeedback(
                "Controlled",
                "Normal",
                "Progressive",
                "Restful",
                "Stable",
                "Positive"),
            90,
            85,
            new DateOnly(2026, 8, 5),
            Now);

        await using var context = _fixture.CreateContext(tenant.TrainerId);
        context.CheckIns.Add(checkIn);
        await context.SaveChangesAsync(cancellationToken);
        context.ChangeTracker.Clear();

        // Act
        var stored = await context.CheckIns
            .AsNoTracking()
            .SingleAsync(value => value.Id == checkIn.Id, cancellationToken);

        // Assert
        Assert.Equal(
            ((decimal?)80.1m, (string?)"Controlled", (string?)"Restful"),
            (stored.BodyMeasurements.WaistCm,
                stored.Feedback.Appetite,
                stored.Feedback.RecoverySleep));
    }

    [Fact]
    public async Task MealPlan_WhenReloaded_PreservesCalculationSnapshot()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(
            Guid.NewGuid().ToString("N"), cancellationToken);
        var macros = MacroTargetCalculator.CalculateFromManualGrams(
            2_000m,
            new ManualMacroInput(150m, 200m, 66.67m));
        var snapshot = NutritionCalculationSnapshot.FromManualEnergy(80m, macros, Now);
        var plan = new MealPlan(
            tenant.TrainerId,
            tenant.ClientId,
            "Round-trip plan",
            null,
            new DateOnly(2026, 8, 5),
            null,
            snapshot,
            Now);

        await using var context = _fixture.CreateContext(tenant.TrainerId);
        context.MealPlans.Add(plan);
        await context.SaveChangesAsync(cancellationToken);
        context.ChangeTracker.Clear();

        // Act
        var stored = await context.MealPlans
            .AsNoTracking()
            .SingleAsync(value => value.Id == plan.Id, cancellationToken);

        // Assert
        Assert.Equal(
            (NutritionCalculationSnapshot.CurrentSchemaVersion,
                NutritionCalculationSnapshot.ManualEnergyOrigin,
                2_000m,
                150m,
                200m,
                66.67m),
            (stored.CalculationSnapshot.SchemaVersion,
                stored.CalculationSnapshot.CalculationOrigin,
                stored.CalculationSnapshot.TargetKcal,
                stored.CalculationSnapshot.ProteinTargetGrams,
                stored.CalculationSnapshot.CarbsTargetGrams,
                stored.CalculationSnapshot.FatsTargetGrams));
    }

    [Fact]
    public async Task Foods_WhenNutrientsExceedOneHundred_ConstraintRejectsInsert()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        const string sql = """
            INSERT INTO foods (
                id, owner_trainer_id, name, description,
                protein, carbs, fats, fiber, is_deleted, created_at, updated_at)
            VALUES (
                @id, NULL, 'Invalid food', NULL,
                50, 40, 20, NULL, false, @now, @now);
        """;

        // Act
        var action = () => _fixture.ExecuteSqlAsync(
            sql,
            cancellationToken,
            new NpgsqlParameter("id", Guid.NewGuid()),
            new NpgsqlParameter("now", Now));

        // Assert
        var exception = await Assert.ThrowsAsync<PostgresException>(action);
        Assert.Equal(
            (PostgresErrorCodes.CheckViolation, "ck_foods_nutrients_per_100g"),
            (exception.SqlState, exception.ConstraintName));
    }

    [Theory]
    [InlineData("uq_client_supplement_active")]
    [InlineData("idx_jobs_first_attempt")]
    [InlineData("idx_jobs_retry")]
    [InlineData("idx_jobs_lease")]
    [InlineData("idx_outbox_first_attempt")]
    [InlineData("idx_outbox_retry")]
    [InlineData("idx_outbox_lease")]
    [InlineData("idx_pack_types_tenant_name_active")]
    [InlineData("idx_client_session_packs_usable_order")]
    [InlineData("idx_sessions_tenant_scheduled_at")]
    public async Task InitialCreate_WhenApplied_CreatesCriticalIndexes(string indexName)
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM pg_indexes
                WHERE schemaname = 'public'
                    AND indexname = @index_name
            );
        """;

        // Act
        var exists = await _fixture.QueryScalarAsync<bool>(
            sql,
            cancellationToken,
            new NpgsqlParameter("index_name", indexName));

        // Assert
        Assert.True(exists);
    }

    [Theory]
    [InlineData("ck_client_session_packs_balance")]
    [InlineData("ck_client_session_packs_price_non_negative")]
    [InlineData("ck_client_session_packs_expected_end_order")]
    [InlineData("ck_client_session_packs_completion_consistency")]
    [InlineData("ck_pack_types_session_count_positive")]
    [InlineData("ck_pack_types_price_non_negative")]
    [InlineData("ck_pack_types_expected_duration_positive")]
    public async Task Migrations_WhenApplied_CreatePackConstraints(
        string constraintName
    )
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM pg_constraint
                WHERE conname = @constraint_name
                    AND contype = 'c'
            );
        """;

        var exists = await _fixture.QueryScalarAsync<bool>(
            sql,
            cancellationToken,
            new NpgsqlParameter("constraint_name", constraintName)
        );

        Assert.True(exists);
    }

    [Theory]
    [InlineData("fk_refresh_tokens_user", "c")]
    [InlineData("fk_client_session_packs_pack_type_tenant", "r")]
    [InlineData("fk_client_supplement_assignments_supplement", "r")]
    public async Task InitialCreate_WhenApplied_CreatesExpectedForeignKeyDeleteBehavior(
        string constraintName,
        string expectedDeleteBehavior)
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        const string sql = """
            SELECT constraint_row.confdeltype::text
            FROM pg_constraint AS constraint_row
            WHERE constraint_row.conname = @constraint_name
                AND constraint_row.contype = 'f';
        """;

        // Act
        var actual = await _fixture.QueryScalarAsync<string>(
            sql,
            cancellationToken,
            new NpgsqlParameter("constraint_name", constraintName));

        // Assert
        Assert.Equal(expectedDeleteBehavior, actual);
    }

    [Theory]
    [InlineData("foods", "is_active")]
    [InlineData("supplements", "serving_size")]
    [InlineData("supplements", "timing")]
    [InlineData("supplements", "is_active")]
    [InlineData("pack_types", "currency")]
    [InlineData("pack_types", "is_active")]
    [InlineData("client_session_packs", "pack_name")]
    [InlineData("client_session_packs", "total_sessions")]
    [InlineData("client_session_packs", "price_cents")]
    [InlineData("client_session_packs", "currency")]
    [InlineData("trainer_settings", "time_zone_id")]
    public async Task InitialCreate_WhenApplied_CreatesRequiredCorrectedColumn(
        string tableName,
        string columnName)
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        const string sql = """
            SELECT is_nullable = 'NO'
            FROM information_schema.columns
            WHERE table_schema = 'public'
                AND table_name = @table_name
                AND column_name = @column_name;
        """;

        // Act
        var isRequired = await _fixture.QueryScalarAsync<bool>(
            sql,
            cancellationToken,
            new NpgsqlParameter("table_name", tableName),
            new NpgsqlParameter("column_name", columnName));

        // Assert
        Assert.True(isRequired);
    }
}
