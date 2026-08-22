using Infrastructure.IntegrationTests.Support;
using Npgsql;

namespace Infrastructure.IntegrationTests.Tenancy;

[Collection(PostgresCollection.Name)]
public sealed class CrossTenantConstraintTests
{
    private static readonly DateTime Now = new(2026, 8, 5, 13, 0, 0, DateTimeKind.Utc);

    private readonly PostgresContainerFixture _fixture;

    public CrossTenantConstraintTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public static TheoryData<string, string> CrossTenantWrites => new()
    {
        {
            """
            INSERT INTO meal_plans (
                id, owner_trainer_id, client_id, name, starts_date,
                kcal_target, protein_target_g, carbs_target_g, fats_target_g,
                calculation_snapshot, created_at, updated_at)
            VALUES (
                @id, @owner_trainer_id, @client_id, 'Cross tenant', DATE '2026-08-05',
                2000, 150, 200, 66.67, '{}'::jsonb, @now, @now);
            """,
            "fk_meal_plans_client_tenant"
        },
        {
            """
            INSERT INTO training_plans (
                id, owner_trainer_id, client_id, name, starts_date, created_at, updated_at)
            VALUES (
                @id, @owner_trainer_id, @client_id, 'Cross tenant', DATE '2026-08-05', @now, @now);
            """,
            "fk_training_plans_client_tenant"
        },
        {
            """
            INSERT INTO initial_assessments (
                id, owner_trainer_id, client_id, weight_kg, height_cm,
                fitness_level, activity_level, goals, body_measurements,
                nutrition_intake, created_at, updated_at)
            VALUES (
                @id, @owner_trainer_id, @client_id, 80, 180,
                'intermediate', 'moderately_active', 'Strength', '{}'::jsonb,
                '{}'::jsonb, @now, @now);
            """,
            "fk_initial_assessments_client_tenant"
        },
        {
            """
            INSERT INTO checkins (
                id, owner_trainer_id, client_id, check_in_date,
                body_measurements, feedback, created_at, updated_at)
            VALUES (
                @id, @owner_trainer_id, @client_id, DATE '2026-08-05',
                '{}'::jsonb, '{}'::jsonb, @now, @now);
            """,
            "fk_checkins_client_tenant"
        },
        {
            """
            INSERT INTO client_supplement_assignments (
                id, owner_trainer_id, client_id, supplement_id,
                serving_size, timing, created_at, updated_at)
            VALUES (
                @id, @owner_trainer_id, @client_id, @supplement_id,
                '5 g', 'Daily', @now, @now);
            """,
            "fk_client_supplement_assignments_client_tenant"
        },
        {
            """
            INSERT INTO sessions (
                id, owner_trainer_id, client_id, starts_at, duration_minutes,
                status, status_changed_at, created_at, updated_at)
            VALUES (
                @id, @owner_trainer_id, @client_id, @now, 60,
                'scheduled', @now, @now, @now);
            """,
            "fk_sessions_client_tenant"
        },
        {
            """
            INSERT INTO client_session_packs (
                id, owner_trainer_id, client_id, pack_type_id, pack_name,
                total_sessions, sessions_remaining, price_cents, currency,
                purchase_date, created_at, updated_at)
            VALUES (
                @id, @owner_trainer_id, @client_id, @pack_type_id, 'Pack 10',
                10, 10, 10000, 'EUR', DATE '2026-08-05', @now, @now);
            """,
            "fk_client_session_packs_client_tenant"
        },
        {
            """
            INSERT INTO notifications (
                id, owner_trainer_id, client_id, recipient_email,
                notification_type, template_key, created_at, updated_at)
            VALUES (
                @id, @owner_trainer_id, @client_id, 'client@example.test',
                'reminder', 'session_reminder', @now, @now);
            """,
            "fk_notifications_client_tenant"
        },
    };

    [Theory]
    [MemberData(nameof(CrossTenantWrites))]
    public async Task PostgreSql_WhenOwnerAndClientBelongToDifferentTenants_RejectsWrite(
        string sql,
        string expectedConstraint)
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var owner = await _fixture.SeedTenantWithClientAsync(
            $"owner-{Guid.NewGuid():N}", cancellationToken);
        var otherTenant = await _fixture.SeedTenantWithClientAsync(
            $"other-{Guid.NewGuid():N}", cancellationToken);
        var supplementId = Guid.NewGuid();
        var packTypeId = Guid.NewGuid();

        await SeedReferencesAsync(
            owner.TrainerId,
            supplementId,
            packTypeId,
            cancellationToken);

        // Act
        var action = () => _fixture.ExecuteSqlAsync(
            sql,
            cancellationToken,
            new NpgsqlParameter("id", Guid.NewGuid()),
            new NpgsqlParameter("owner_trainer_id", owner.TrainerId),
            new NpgsqlParameter("client_id", otherTenant.ClientId),
            new NpgsqlParameter("supplement_id", supplementId),
            new NpgsqlParameter("pack_type_id", packTypeId),
            new NpgsqlParameter("now", Now));

        // Assert
        var exception = await Assert.ThrowsAsync<PostgresException>(action);
        Assert.Equal(
            (PostgresErrorCodes.ForeignKeyViolation, expectedConstraint),
            (exception.SqlState, exception.ConstraintName));
    }

    private async Task SeedReferencesAsync(
        Guid ownerTrainerId,
        Guid supplementId,
        Guid packTypeId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO supplements (
                id, owner_trainer_id, created_by_user_id, name,
                unit_of_measure, serving_size, timing, created_at, updated_at)
            VALUES (
                @supplement_id, NULL, @owner_trainer_id, 'Global supplement',
                'grams', '5 g', 'Daily', @now, @now);

            INSERT INTO pack_types (
                id, owner_trainer_id, name, session_count, price_cents,
                currency, created_at, updated_at)
            VALUES (
                @pack_type_id, @owner_trainer_id, 'Pack 10', 10, 10000,
                'EUR', @now, @now);
            """;

        await _fixture.ExecuteSqlAsync(
            sql,
            cancellationToken,
            new NpgsqlParameter("supplement_id", supplementId),
            new NpgsqlParameter("pack_type_id", packTypeId),
            new NpgsqlParameter("owner_trainer_id", ownerTrainerId),
            new NpgsqlParameter("now", Now));
    }
}
