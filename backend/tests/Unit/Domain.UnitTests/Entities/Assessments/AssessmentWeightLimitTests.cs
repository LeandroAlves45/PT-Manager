using Domain.Entities.Assessments;
using Domain.Exceptions;
using Domain.ValueObjects;

namespace Domain.UnitTests.Entities.Assessments;

/// <summary>O teto do peso corporal também é uma invariante do domínio.</summary>
public sealed class AssessmentWeightLimitTests
{
    private static readonly DateTime Now = new(2026, 8, 17, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = new(2026, 8, 17);

    [Fact]
    public void CheckIn_SubmitResponse_WeightAboveMaximum_Throws()
    {
        var checkIn = new CheckIn(Guid.NewGuid(), Guid.NewGuid(), Today, null, Now);

        Assert.Throws<DomainException>(() => Submit(checkIn, AssessmentLimits.MaxWeightKg + 0.01m));
    }

    [Fact]
    public void CheckIn_SubmitResponse_WeightAtMaximum_IsAccepted()
    {
        var checkIn = new CheckIn(Guid.NewGuid(), Guid.NewGuid(), Today, null, Now);

        Submit(checkIn, AssessmentLimits.MaxWeightKg);

        Assert.Equal(AssessmentLimits.MaxWeightKg, checkIn.WeightKg);
    }

    private static void Submit(CheckIn checkIn, decimal weightKg) =>
        checkIn.SubmitResponse(
            weightKg,
            null,
            null,
            BodyMeasurements.Empty,
            CheckInFeedback.Empty,
            null,
            null,
            Today,
            Now);
}
