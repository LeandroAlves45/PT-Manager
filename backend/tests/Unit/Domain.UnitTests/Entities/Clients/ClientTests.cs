using Domain.Entities.Clients;
using Domain.Exceptions;
using Domain.ValueObjects;
using Xunit;
namespace Domain.UnitTests.Entities.Clients;

public sealed class ClientTests
{
    private static readonly DateTime Now = new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);
    private static readonly BirthDate ValidBirthDate =
        BirthDate.Create(new DateOnly(1995, 8, 1), DateOnly.FromDateTime(Now));

    [Fact]
    public void Constructor_WithRequiredDemographicData_StoresValueObject()
    {
        // Arrange
        var client = CreateClient(ValidBirthDate, BiologicalSex.Male);

        // Assert
        Assert.Equal(ValidBirthDate, client.BirthDate);
        Assert.Equal(BiologicalSex.Male, client.Sex);
        Assert.Null(client.UserId);
    }

    [Fact]
    public void Constructor_WithoutBirthDate_ThrowsDomainException()
    {
        // Act
        var action = () => CreateClient(null!, BiologicalSex.Male);

        // Assert
        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void Constructor_WithoutSex_ThrowsDomainException()
    {
        // Act
        var action = () => CreateClient(ValidBirthDate, null!);

        // Assert
        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void Constructor_WhenAccountDoesNotExist_LeavesUserIdEmpty()
    {
        // Arrange
        var client = CreateClient(ValidBirthDate, BiologicalSex.Male);

        // Assert
        Assert.Null(client.UserId);
    }

    [Fact]
    public void AttachUser_WhenClientHasNoAccount_AssociatesUser()
    {
        // Arrange
        var client = CreateClient(ValidBirthDate, BiologicalSex.Male);
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
        var client = CreateClient(ValidBirthDate, BiologicalSex.Male);
        client.AttachUser(Guid.NewGuid(), Now.AddMinutes(1));

        // Act & Assert
        Assert.Throws<DomainException>(() => client.AttachUser(Guid.NewGuid(), Now.AddMinutes(2)));
    }

    [Fact]
    public void AttachUser_WhenUserIdIsEmpty_ThrowsDomainException()
    {
        var client = CreateClient(ValidBirthDate, BiologicalSex.Male);

        var action = () => client.AttachUser(Guid.Empty, Now.AddMinutes(1));

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void Constructor_WhenNameIsNull_ThrowsDomainException()
    {
        var action = () => new Client(
            Guid.NewGuid(), null!, null, "+351912345678", ValidBirthDate, BiologicalSex.Male,
            null, null, null, null, Now);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void Constructor_WhenPhoneIsNull_ThrowsDomainException()
    {
        var action = () => new Client(
            Guid.NewGuid(), "John Doe", null, null!, ValidBirthDate, BiologicalSex.Male,
            null, null, null, null, Now);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void Constructor_WhenPersistedBirthDateIsInTheFuture_ThrowsDomainException()
    {
        var futureBirthDate = BirthDate.FromPersisted(new DateOnly(2026, 8, 2));
        // Arrange
        var action = () => CreateClient(futureBirthDate, BiologicalSex.Male);

        // Act & Assert
        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void UpdateProfile_WithCorrectedSex_ReplacesSex()
    {
        // Arrange
        var client = CreateClient(ValidBirthDate, BiologicalSex.Male);

        // Act
        client.UpdateProfile(
            "John Doe",
            "test@example.com",
            "+351912345678",
            ValidBirthDate,
            BiologicalSex.Female,
            "Strength",
            null,
            null,
            null,
            Now.AddMinutes(1)
        );

        // Assert
        Assert.Equal(BiologicalSex.Female, client.Sex);
    }

    private static Client CreateClient(BirthDate birthDate, BiologicalSex sex) => new(
        Guid.NewGuid(),
        "John Doe",
        "test@example.com",
        "+351912345678",
        birthDate,
        sex,
        "Strength",
        null,
        null,
        null,
        Now
    );

    private static Client CreateValidClient() => CreateClient(BirthDate.Create(
        new DateOnly(1995, 8, 1), DateOnly.FromDateTime(Now)), BiologicalSex.Male);
}
