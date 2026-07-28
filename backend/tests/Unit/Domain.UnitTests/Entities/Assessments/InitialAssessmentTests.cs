using Domain.Entities.Assessments;
using Domain.Exceptions;
using Xunit;

namespace Domain.UnitTests.Entities.Assessments;

public sealed class InitialAssessmentTests
{
    [Fact]
    public void Update_DeletedAssessment_ThrowsDomainException()
    {
        // Arrange
        var now = new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);
        var assessment = new InitialAssessment(
            Guid.NewGuid(),
            Guid.NewGuid(),
            30,
            "male",
            80,
            180,
            null,
            null,
            "active",
            "Build strength",
            now
        );
        assessment.SoftDelete(now);

        // Act & Assert
        Assert.Throws<DomainException>(() => assessment.Update(
            31,
            "male",
            79,
            180,
            null,
            null,
            "active",
            "Build strength",
            now.AddMinutes(1)
        ));
    }
}
