using System.Text;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Api.FunctionalTests.Controllers;

using Api.FunctionalTests.Support;

/// <summary>
/// Mede, com <c>EXPLAIN (ANALYZE, BUFFERS)</c> em PostgreSQL real, o plano das
/// ordenações das listagens.
/// </summary>
[Collection(ApiTestCollection.Name)]
public sealed class TrainingListingQueryPlanTests
{
    private readonly PostgresApiFixture _fixture;

    public TrainingListingQueryPlanTests(PostgresApiFixture fixture) => _fixture = fixture;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public static TheoryData<string, string> Listings()
    {
        var data = new TheoryData<string, string>
        {
            {
                "ListExerciseSetLogs",
                """
                SELECT id FROM client_exercise_set_logs
                WHERE client_id = @tenant
                ORDER BY performed_at DESC, id
                LIMIT 50
                """
            },
            {
                "ListSessions",
                """
                SELECT id FROM sessions
                WHERE owner_trainer_id = @tenant AND is_deleted = false
                ORDER BY starts_at, id
                LIMIT 50
                """
            },
            {
                "ListExercises",
                """
                SELECT id FROM exercises
                WHERE owner_trainer_id = @tenant OR owner_trainer_id IS NULL
                ORDER BY name, (owner_trainer_id IS NULL), id
                LIMIT 50
                """
            },
            {
                "ListTrainingPlans",
                """
                SELECT id FROM training_plans
                WHERE owner_trainer_id = @tenant AND is_deleted = false
                ORDER BY starts_date DESC, created_at DESC, id
                LIMIT 50
                """
            },
            {
                "ListPackTypes",
                """
                SELECT id FROM pack_types
                WHERE owner_trainer_id = @tenant AND is_deleted = false
                ORDER BY name, created_at, id
                LIMIT 50
                """
            },
            {
                "ListClientSessionPacks",
                """
                SELECT id FROM client_session_packs
                WHERE owner_trainer_id = @tenant AND is_deleted = false
                ORDER BY expected_end_date, created_at, id
                LIMIT 50
                """
            }
        };

        return data;
    }

    [Theory]
    [MemberData(nameof(Listings))]
    public async Task Listing_OrdersWithoutSpillingToDisk(string listing, string sql)
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);

        var plan = await ExplainAsync(sql, tenant.TrainerId);

        Assert.DoesNotContain(
            "Sort Method: external",
            plan,
            StringComparison.Ordinal);

        Assert.False(
            string.IsNullOrWhiteSpace(plan),
            $"{listing} não devolveu plano de execução.");
    }

    private async Task<string> ExplainAsync(string sql, Guid tenantId)
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();

        var connection = (NpgsqlConnection)context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(Token);

        await using var command = connection.CreateCommand();
        command.CommandText = $"EXPLAIN (ANALYZE, BUFFERS) {sql}";
        command.Parameters.AddWithValue("tenant", tenantId);

        var builder = new StringBuilder();
        await using var reader = await command.ExecuteReaderAsync(Token);
        while (await reader.ReadAsync(Token))
            builder.AppendLine(reader.GetString(0));

        return builder.ToString();
    }
}
