using Domain.Entities.Supplements;
using Domain.Exceptions;

namespace Domain.UnitTests.Entities.Supplements;

public sealed class ClientSupplementAssignmentTests
{
    private static readonly DateTime Now = new(2026, 8, 18, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Constructor_NormalizesSnapshotAndStartsActive()
    {
        var assignment = CreateAssignment(" 5 g ", " breakfast ", " client note ");

        Assert.Equal(("5 g", "breakfast", "client note", true),
            (assignment.ServingSize, assignment.Timing, assignment.TrainerNotes,
                assignment.IsActive));
    }

    [Fact]
    public void UpdateInstructions_ReplacesOnlyAssignmentSnapshot()
    {
        var assignment = CreateAssignment("5 g", "breakfast", null);

        assignment.UpdateInstructions(" 3 g ", " evening ", " adjusted ", Now.AddMinutes(1));

        Assert.Equal(("3 g", "evening", "adjusted"),
            (assignment.ServingSize, assignment.Timing, assignment.TrainerNotes));
    }

    [Fact]
    public void DeactivateAndReactivate_PreserveTheSameAssignment()
    {
        var assignment = CreateAssignment("5 g", "breakfast", null);
        var id = assignment.Id;

        assignment.Deactivate(Now.AddMinutes(1));
        Assert.False(assignment.IsActive);
        assignment.Reactivate(Now.AddMinutes(2));

        Assert.True(assignment.IsActive);
        Assert.Equal(id, assignment.Id);
    }

    [Fact]
    public void Constructor_WhenIdentityIsMissing_ThrowsDomainException()
    {
        var action = () => new ClientSupplementAssignment(
            Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), "5 g", "daily", null, Now);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void PublicSurface_DoesNotExposeSoftDelete()
    {
        var properties = typeof(ClientSupplementAssignment).GetProperties()
            .Select(property => property.Name);
        var methods = typeof(ClientSupplementAssignment).GetMethods()
            .Select(method => method.Name);

        Assert.DoesNotContain("IsDeleted", properties);
        Assert.DoesNotContain("SoftDelete", methods);
    }

    private static ClientSupplementAssignment CreateAssignment(
        string servingSize, string timing, string? trainerNotes) => new(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), servingSize,
            timing, trainerNotes, Now);
}
