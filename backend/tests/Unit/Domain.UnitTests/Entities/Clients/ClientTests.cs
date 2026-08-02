using Domain.Entities.Clients;
using Domain.Exceptions;
using Xunit;
namespace Domain.UnitTests.Entities.Clients;

public sealed class ClientTests
{
    private static readonly DateTime Now = new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Constructor_WhenAccountDoesNotExist_LeavesUserIdEmpty()
    {
        // Arrange
        var client = CreateClient();

        // Assert
        Assert.Null(client.UserId);
    }

    [Fact]
    public void AttachUser_WhenClientHasNoAccount_AssociatesUser()
    {
        // Arrange
        var client = CreateClient();
        var userId = Guid.NewGuid();

        // Act
        client.AttachUser(userId, Now.AddMinutes(1));

        // Assert
        Assert.Equal(userId, client.UserId);
    }

    [Fact]
    public void AttachUser_WhenAccountAlreadyExists_ThrowsDomainException()
    {
        // Arrange
        var client = CreateClient();
        client.AttachUser(Guid.NewGuid(), Now.AddMinutes(1));

        // Act & Assert
        Assert.Throws<DomainException>(() => client.AttachUser(Guid.NewGuid(), Now.AddMinutes(2)));
    }

    [Fact]
    public void AttachUser_WhenUserIdIsEmpty_ThrowsDomainException()
    {
        var client = CreateClient();

        var action = () => client.AttachUser(Guid.Empty, Now.AddMinutes(1));

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void Constructor_WhenNameIsNull_ThrowsDomainException()
    {
        var action = () => new Client(
            Guid.NewGuid(), null!, null, "+351912345678", null, null,
            null, null, null, null, Now);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void Constructor_WhenPhoneIsNull_ThrowsDomainException()
    {
        var action = () => new Client(
            Guid.NewGuid(), "John Doe", null, null!, null, null,
            null, null, null, null, Now);

        Assert.Throws<DomainException>(action);
    }



    [Fact]
    public void Constructor_WhenBirthDateIsInTheFuture_ThrowsDomainException()
    {
        // Arrange
        var action = () => new Client(
            Guid.NewGuid(),
            "John Doe",
            "test@example.com",
            "+351912345678",
            new DateOnly(2026, 8, 2),
            "M",
            null,
            null,
            null,
            null,
            Now
        );
        // Act & Assert
        Assert.Throws<DomainException>(action);
    }

    private static Client CreateClient() => new(
        Guid.NewGuid(),
        "John Doe",
        "test@example.com",
        "+351912345678",
        new DateOnly(1995, 8, 1),
        "M",
        "Strength",
        null,
        null,
        null,
        Now
    );
}
