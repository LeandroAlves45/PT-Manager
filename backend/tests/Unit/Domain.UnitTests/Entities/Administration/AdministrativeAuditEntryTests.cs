using Domain.Entities.Administration;
using Domain.Exceptions;

namespace Domain.UnitTests.Entities.Administration;

public sealed class AdministrativeAuditEntryTests
{
    private static readonly DateTime Now = new(2026, 8, 18, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Constructor_NormalizesMetadataAndPreservesSnapshots()
    {
        var actor = Guid.NewGuid();
        var resource = Guid.NewGuid();

        var entry = new AdministrativeAuditEntry(
            actor, " create ", " supplement ", resource, null, "{\"name\":\"Creatine\"}", Now);

        Assert.Equal((actor, "create", "supplement", resource, Now),
            (entry.ActorUserId, entry.Action, entry.ResourceType, entry.ResourceId,
                entry.OccurredAt));
        Assert.Null(entry.BeforeState);
        Assert.NotNull(entry.AfterState);
    }

    [Fact]
    public void Constructor_WhenBothSnapshotsAreMissing_ThrowsDomainException()
    {
        var action = () => new AdministrativeAuditEntry(
            Guid.NewGuid(), "update", "supplement", Guid.NewGuid(), null, null, Now);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void Constructor_WhenBothSnapshotsContainOnlyWhitespace_ThrowsDomainException()
    {
        var action = () => new AdministrativeAuditEntry(
            Guid.NewGuid(), "update", "supplement", Guid.NewGuid(), "   ", "\t", Now);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void PublicSurface_HasNoMutationOrDeletionMethods()
    {
        var publicMethods = typeof(AdministrativeAuditEntry).GetMethods()
            .Where(method => method.DeclaringType == typeof(AdministrativeAuditEntry) &&
                !method.IsSpecialName)
            .Select(method => method.Name)
            .ToArray();

        Assert.Empty(publicMethods);
        Assert.All(typeof(AdministrativeAuditEntry).GetProperties(),
            property => Assert.False(property.SetMethod?.IsPublic ?? false));
    }
}
