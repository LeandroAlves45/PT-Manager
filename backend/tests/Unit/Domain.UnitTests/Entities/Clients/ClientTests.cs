using Domain.Entities.Clients;
using Domain.Exceptions;
using Xunit;

namespace Domain.UnitTests.Entities.Clients;

public sealed class ClientTests
{
    [Fact]
    public void Reactivate_DeletedClient_ThrowsDomainException()
    {
        // Arrange
        var now = new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);
        var client = new Client(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Test Client",
            null,
            now
        );
        client.SoftDelete(now);

        // Act & Assert
        Assert.Throws<DomainException>(() => client.Reactivate(now.AddMinutes(1)));
    }
}
