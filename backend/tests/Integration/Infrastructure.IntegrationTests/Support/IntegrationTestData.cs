using Domain.Entities.Jobs;
using Domain.Entities.Nutrition;
using Domain.Entities.Supplements;
using Domain.Entities.Training;

namespace Infrastructure.IntegrationTests.Support;

/// <summary>Cria entidades válidas e pequenas para testes de integração.</summary>
internal static class IntegrationTestData
{
    public static DurableJob Job(
        DateTime scheduledAt, DateTime now, string? idempotencyKey = null) => new(
            null, "integration_test", 1, "{\"value\":1}",
            idempotencyKey ?? Guid.NewGuid().ToString("N"), Guid.NewGuid(),
            scheduledAt, now);

    public static OutboxMessage Message(
        DateTime now, string? idempotencyKey = null) => new(
            null, "integration_test", "{\"value\":1}",
            idempotencyKey ?? Guid.NewGuid().ToString("N"), Guid.NewGuid(), now);

    public static Food Food(Guid? ownerTrainerId, DateTime now) =>
        new(ownerTrainerId, "Rice", null, 2.7m, 28m, 0.3m, 0.4m, now);

    public static Exercise Exercise(Guid? ownerTrainerId, DateTime now) =>
        new(ownerTrainerId, "Squat", null, "legs", "barbell", "medium", null, now);

    public static Supplement Supplement(
        Guid? ownerTrainerId, DateTime now, Guid? createdByUserId = null) => new(
            ownerTrainerId, createdByUserId ?? ownerTrainerId ?? Guid.NewGuid(),
            "Creatine", null, "grams", "5 g", "Daily", null, now);
}
