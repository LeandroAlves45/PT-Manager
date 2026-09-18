using Application.Features.Assessments.CheckIns.Abstractions;
using Domain.Entities.Assessments;
using Infrastructure.IntegrationTests.Support;
using Infrastructure.Persistence.Assessments;
using Infrastructure.Persistence.Errors;
using Infrastructure.Time;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.IntegrationTests.Assessments;

/// <summary>Prova a marcação idempotente de check-in revisto e a constraint da coluna.</summary>
[Collection(PostgresCollection.Name)]
public sealed class CheckInReviewStoreTests
{
    private static readonly DateTime Now = new(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = new(2026, 9, 16);

    private readonly PostgresContainerFixture _fixture;

    public CheckInReviewStoreTests(PostgresContainerFixture fixture) => _fixture = fixture;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task MarkReviewed_Unanswered_ReturnsNotAnswered()
    {
        var (trainerId, checkInId) = await SeedAsync(answered: false);
        await using var context = _fixture.CreateContext(trainerId);

        var result = await Store(context).MarkReviewedAsync(trainerId, checkInId, Now, Token);

        Assert.Equal(CheckInStoreResult.Status.NotAnswered, result.Kind);
    }

    [Fact]
    public async Task MarkReviewed_AnsweredTwice_KeepsFirstInstant()
    {
        var (trainerId, checkInId) = await SeedAsync(answered: true);
        await using var context = _fixture.CreateContext(trainerId);
        var store = Store(context);

        var first = await store.MarkReviewedAsync(trainerId, checkInId, Now.AddHours(1), Token);
        var second = await store.MarkReviewedAsync(trainerId, checkInId, Now.AddHours(2), Token);

        Assert.Equal(CheckInStoreResult.Status.Reviewed, first.Kind);
        Assert.Equal(CheckInStoreResult.Status.AlreadyInRequestedState, second.Kind);
        var stored = await context.CheckIns.AsNoTracking().SingleAsync(item => item.Id == checkInId, Token);
        Assert.Equal(Now.AddHours(1), stored.ReviewedAt);
    }

    [Fact]
    public async Task MarkReviewed_CheckInOfAnotherTenant_ReturnsNotFound()
    {
        var (_, checkInId) = await SeedAsync(answered: true);
        var intruder = await _fixture.SeedTenantWithClientAsync($"rev-int-{Guid.NewGuid():N}", Token);
        await using var context = _fixture.CreateContext(intruder.TrainerId);

        var result = await Store(context).MarkReviewedAsync(intruder.TrainerId, checkInId, Now, Token);

        Assert.Equal(CheckInStoreResult.Status.CheckInNotFound, result.Kind);
    }

    [Fact]
    public async Task Database_RejectsReviewedAtOnUnansweredCheckIn()
    {
        var (_, checkInId) = await SeedAsync(answered: false);

        var exception = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => _fixture.ExecuteSqlAsync(
            $"UPDATE checkins SET reviewed_at = now() WHERE id = '{checkInId}'", Token));

        Assert.Equal("ck_checkins_review_requires_response", exception.ConstraintName);
    }

    private static CheckInStore Store(Infrastructure.Data.PtManagerDbContext context) =>
        new(context, new TrainerTimeZoneProvider(context), new PostgresConstraintTranslator());

    private async Task<(Guid TrainerId, Guid CheckInId)> SeedAsync(bool answered)
    {
        var tenant = await _fixture.SeedTenantWithClientAsync($"rev-{Guid.NewGuid():N}", Token);
        await using var context = _fixture.CreateContext(tenant.TrainerId);
        var checkIn = new CheckIn(tenant.TrainerId, tenant.ClientId, Today, null, Now);
        if (answered)
            checkIn.SubmitResponse(72m, null, null, null, null, null, null, Today, Now);
        context.CheckIns.Add(checkIn);
        await context.SaveChangesAsync(Token);
        return (tenant.TrainerId, checkIn.Id);
    }
}
