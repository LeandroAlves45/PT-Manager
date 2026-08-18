using Domain.Entities.Assessments;
using Domain.Exceptions;
using Domain.ValueObjects;

namespace Domain.UnitTests.Entities.Assessments;

public sealed class CheckInTests
{
    private static readonly Guid TrainerId = Guid.NewGuid();
    private static readonly Guid ClientId = Guid.NewGuid();
    private static readonly DateTime Now =
        new(2026, 8, 17, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = new(2026, 8, 17);

    [Fact]
    public void Constructor_CreatesEmptyScheduledCheckIn()
    {
        var checkIn = Create(Today.AddDays(1));

        Assert.Equal(
            (null, null, null, BodyMeasurements.Empty, CheckInFeedback.Empty),
            (checkIn.WeightKg, checkIn.RespondedAt, checkIn.CancelledAt,
                checkIn.BodyMeasurements, checkIn.Feedback));
    }

    [Fact]
    public void Constructor_TargetBeforeCheckInDate_Throws()
    {
        var action = () => new CheckIn(
            TrainerId,
            ClientId,
            Today,
            Today.AddDays(-1),
            Now);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void Reschedule_BeforeScheduledDay_ChangesDate()
    {
        var checkIn = Create(Today.AddDays(2));

        checkIn.Reschedule(Today.AddDays(3), Today.AddDays(10), Today, Now);

        Assert.Equal(
            (Today.AddDays(3), (DateOnly?)Today.AddDays(10)),
            (checkIn.CheckInDate, checkIn.TargetDate));
    }

    [Fact]
    public void Reschedule_OnScheduledDay_Throws()
    {
        var checkIn = Create(Today);

        Assert.Throws<DomainException>(() =>
            checkIn.Reschedule(Today.AddDays(1), null, Today, Now));
    }

    [Fact]
    public void Reschedule_RepeatedWithSameIntent_PreservesUpdatedAt()
    {
        var checkIn = Create(Today.AddDays(2));

        checkIn.Reschedule(Today.AddDays(3), null, Today, Now.AddMinutes(1));
        var updatedAt = checkIn.UpdatedAt;
        checkIn.Reschedule(Today.AddDays(3), null, Today, Now.AddMinutes(2));

        Assert.Equal(updatedAt, checkIn.UpdatedAt);
    }

    [Fact]
    public void Cancel_RepeatedWithSameIntent_IsIdempotent()
    {
        var checkIn = Create(Today.AddDays(1));
        checkIn.Cancel(Today, Now);

        checkIn.Cancel(Today, Now.AddMinutes(1));

        Assert.Equal((Now, Now), (checkIn.CancelledAt, checkIn.UpdatedAt));
    }

    [Fact]
    public void SubmitResponse_OnScheduledDay_StoresRequiredWeight()
    {
        var checkIn = Create(Today);

        Submit(checkIn, 80m, Now);

        Assert.Equal((80m, Now, null),
            (checkIn.WeightKg, checkIn.RespondedAt, checkIn.CancelledAt));
    }

    [Fact]
    public void SubmitResponse_ExactRetry_PreservesTimestamps()
    {
        var checkIn = Create(Today);
        Submit(checkIn, 80m, Now);

        Submit(checkIn, 80m, Now.AddMinutes(5));

        Assert.Equal((Now, Now), (checkIn.RespondedAt, checkIn.UpdatedAt));
    }

    [Fact]
    public void SubmitResponse_DifferentRetry_Throws()
    {
        var checkIn = Create(Today);
        Submit(checkIn, 80m, Now);

        Assert.Throws<DomainException>(() =>
            Submit(checkIn, 81m, Now.AddMinutes(1)));
    }

    [Fact]
    public void SubmitResponse_OutsideScheduledDay_Throws()
    {
        var checkIn = Create(Today.AddDays(1));

        Assert.Throws<DomainException>(() => Submit(checkIn, 80m, Now));
    }

    [Fact]
    public void SubmitResponse_CancelledCheckIn_Throws()
    {
        var checkIn = Create(Today.AddDays(1));
        checkIn.Cancel(Today, Now);

        Assert.Throws<DomainException>(() =>
            checkIn.SubmitResponse(
                80m,
                null,
                null,
                null,
                null,
                null,
                null,
                Today.AddDays(1),
                Now.AddDays(1)));
    }

    [Fact]
    public void SubmitResponse_DeletedCheckIn_Throws()
    {
        var checkIn = Create(Today);
        checkIn.SoftDelete(Now);

        Assert.Throws<DomainException>(() => Submit(checkIn, 80m, Now));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void SubmitResponse_BodyFatExclusiveBoundary_Throws(int bodyFatPercentage)
    {
        var checkIn = Create(Today);

        Assert.Throws<DomainException>(() =>
            checkIn.SubmitResponse(
                80m,
                bodyFatPercentage,
                null,
                null,
                null,
                null,
                null,
                Today,
                Now));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void SubmitResponse_AdherenceBoundary_IsAccepted(int score)
    {
        var checkIn = Create(Today);

        checkIn.SubmitResponse(
            80m,
            null,
            null,
            null,
            null,
            score,
            score,
            Today,
            Now);

        Assert.Equal(
            (score, score),
            (checkIn.TrainingAdherenceScore, checkIn.NutritionAdherenceScore));
    }

    [Fact]
    public void Correct_PreservesScheduleResponseAndCreationTimestamps()
    {
        var checkIn = Create(Today);
        Submit(checkIn, 80m, Now);
        var createdAt = checkIn.CreatedAt;
        var respondedAt = checkIn.RespondedAt;

        checkIn.Correct(
            Today.AddDays(14),
            79m,
            20m,
            "corrected",
            BodyMeasurements.Empty,
            CheckInFeedback.Empty,
            90,
            85,
            Now.AddHours(1));

        Assert.Equal(
            (Today, createdAt, respondedAt, 79m),
            (checkIn.CheckInDate, checkIn.CreatedAt, checkIn.RespondedAt,
                checkIn.WeightKg));
    }

    [Fact]
    public void Correct_UnansweredCheckIn_Throws()
    {
        var checkIn = Create(Today);

        Assert.Throws<DomainException>(() => checkIn.Correct(
            null,
            80m,
            null,
            null,
            null,
            null,
            null,
            null,
            Now));
    }

    private static CheckIn Create(DateOnly date) =>
        new(TrainerId, ClientId, date, null, Now);

    private static void Submit(CheckIn checkIn, decimal weight, DateTime now) =>
        checkIn.SubmitResponse(
            weight,
            null,
            null,
            BodyMeasurements.Empty,
            CheckInFeedback.Empty,
            null,
            null,
            Today,
            now);
}
