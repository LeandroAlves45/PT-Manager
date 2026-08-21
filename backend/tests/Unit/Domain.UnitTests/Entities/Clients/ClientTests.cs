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
        // Arrange
        var futureBirthDate = BirthDate.FromPersisted(new DateOnly(2026, 8, 5));

        // Act & Assert
        Assert.Throws<DomainException>(() => CreateClient(futureBirthDate, BiologicalSex.Male));
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

    [Fact]
    public void Deactivate_ActiveClient_SetsIsActiveFalseAndUpdatesTimestamp()
    {
        // Arrange
        var client = CreateValidClient();

        // Act
        client.Deactivate(Now.AddMinutes(1));

        // Assert
        Assert.False(client.IsActive);
        Assert.Equal(Now.AddMinutes(1), client.UpdatedAt);
    }

    [Fact]
    public void Deactivate_WhenClientIsDeleted_ThrowsDomainException()
    {
        // Arrange
        var client = CreateValidClient();
        client.SoftDelete(Now.AddMinutes(1));

        // Act
        var action = () => client.Deactivate(Now.AddMinutes(2));

        // Assert
        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void Reactivate_InactiveClient_SetsIsActiveTrueAndUpdatesTimestamp()
    {
        // Arrange
        var client = CreateValidClient();
        client.Deactivate(Now.AddMinutes(1));

        // Act
        client.Reactivate(Now.AddMinutes(2));

        // Assert
        Assert.True(client.IsActive);
        Assert.Equal(Now.AddMinutes(2), client.UpdatedAt);
    }

    [Fact]
    public void Reactivate_WhenClientIsDeleted_ThrowsDomainException()
    {
        // Arrange
        var client = CreateValidClient();
        client.SoftDelete(Now.AddMinutes(1));

        // Act
        var action = () => client.Reactivate(Now.AddMinutes(2));

        // Assert
        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void SoftDelete_ActiveClient_SetsIsDeletedAndDeactivates()
    {
        // Arrange
        var client = CreateValidClient();

        // Act
        client.SoftDelete(Now.AddMinutes(1));

        // Assert
        Assert.True(client.IsDeleted);
        Assert.False(client.IsActive);
    }

    [Fact]
    public void SoftDelete_WhenAlreadyDeleted_IsIdempotent()
    {
        // Arrange: SoftDelete não chama EnsureNotDeleted de propósito —
        // repetir a operação sobre um cliente já apagado nunca deve lançar.
        var client = CreateValidClient();
        client.SoftDelete(Now.AddMinutes(1));

        // Act
        var action = () => client.SoftDelete(Now.AddMinutes(2));

        // Assert
        Assert.Null(Record.Exception(action));
        Assert.True(client.IsDeleted);
        Assert.Equal(Now.AddMinutes(2), client.UpdatedAt);
    }

    [Fact]
    public void SetAvatar_ValidUrl_SetsAvatarUrlAndUpdatesTimestamp()
    {
        // Arrange
        var client = CreateValidClient();

        // Act
        client.SetAvatar("https://cdn.example.com/avatar.png", Now.AddMinutes(1));

        // Assert
        Assert.Equal("https://cdn.example.com/avatar.png", client.AvatarUrl);
        Assert.Equal(Now.AddMinutes(1), client.UpdatedAt);
    }

    [Fact]
    public void SetAvatar_NullOrWhitespace_ClearsAvatarUrl()
    {
        // Arrange
        var client = CreateValidClient();
        client.SetAvatar("https://cdn.example.com/avatar.png", Now.AddMinutes(1));

        // Act
        client.SetAvatar("   ", Now.AddMinutes(2));

        // Assert
        Assert.Null(client.AvatarUrl);
    }

    [Fact]
    public void SetAvatar_ExceedsMaxLength_ThrowsDomainException()
    {
        // Arrange
        var client = CreateValidClient();
        var tooLong = "https://cdn.example.com/" + new string('a', 480) + ".png";

        // Act
        var action = () => client.SetAvatar(tooLong, Now.AddMinutes(1));

        // Assert
        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void SetAvatar_WhenClientIsDeleted_ThrowsDomainException()
    {
        // Arrange
        var client = CreateValidClient();
        client.SoftDelete(Now.AddMinutes(1));

        // Act
        var action = () => client.SetAvatar("https://cdn.example.com/avatar.png", Now.AddMinutes(2));

        // Assert
        Assert.Throws<DomainException>(action);
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
