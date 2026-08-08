using Application.Features.Clients.CreateClient;
using Application.Features.Clients.ListClients;
using Application.Features.Clients.UpdateClient;
using Xunit;

namespace Application.UnitTests.Features.Clients;

/// <summary>Verifica contratos inválidos, limites e códigos estáveis.</summary>
public sealed class ClientValidatorsTests
{
    private readonly StubClock _clock = new StubClock { UtcNow = ClientTestData.NowUtc };

    [Fact]
    public void Create_ValidCommand_Passes()
    {
        // Arrange
        var validator = new CreateClientCommandValidator(_clock);

        // Act
        var result = validator.Validate(ClientTestData.CreateValidCommand());

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Create_EmptyName_ReturnsStableCode()
    {
        // Arrange
        var command = ClientTestData.CreateValidCommand() with { Name = "" };

        // Act
        var result = new CreateClientCommandValidator(_clock).Validate(command);

        // Assert
        Assert.Contains(result.Errors, error =>
            error.PropertyName == "Name" && error.ErrorCode == "client_name_invalid");
    }

    [Fact]
    public void Create_InvalidEmail_ReturnsStableCode()
    {
        // Arrange
        var command = ClientTestData.CreateValidCommand() with { ContactEmail = "invalid-email" };

        // Act
        var result = new CreateClientCommandValidator(_clock).Validate(command);

        // Assert
        Assert.Contains(result.Errors, e =>
            e.PropertyName == "ContactEmail" && e.ErrorCode == "client_email_invalid");
    }

    [Fact]
    public void Create_EmptyPhone_ReturnsStableCode()
    {
        var command = ClientTestData.CreateValidCommand() with { Phone = string.Empty };

        var result = new CreateClientCommandValidator(_clock).Validate(command);

        Assert.Contains(result.Errors, error =>
            error.PropertyName == "Phone" && error.ErrorCode == "client_phone_invalid");
    }

    [Fact]
    public void Create_FutureBirthDate_ReturnsStableCode()
    {
        // Arrange
        var futureBirthDate = DateOnly.FromDateTime(_clock.UtcNow.AddDays(1));
        var command = ClientTestData.CreateValidCommand() with { BirthDate = futureBirthDate };

        // Act
        var result = new CreateClientCommandValidator(_clock).Validate(command);

        // Assert
        Assert.Contains(result.Errors, e =>
            e.PropertyName == "BirthDate" && e.ErrorCode == "client_birth_date_invalid");
    }

    [Fact]
    public void Create_InvalidSex_ReturnsStableCode()
    {
        // Arrange
        var command = ClientTestData.CreateValidCommand() with { Sex = "other" };

        // Act
        var result = new CreateClientCommandValidator(_clock).Validate(command);

        // Assert
        Assert.Contains(result.Errors, e =>
            e.PropertyName == "Sex" && e.ErrorCode == "client_sex_invalid");
    }

    [Fact]
    public void Update_EmptyId_ReturnsStableCode()
    {
        // Arrange
        var command = ClientTestData.CreateValidUpdateCommand(Guid.Empty);

        // Act
        var result = new UpdateClientCommandValidator(_clock).Validate(command);

        // Assert
        Assert.Contains(result.Errors, e =>
            e.PropertyName == "ClientId" && e.ErrorCode == "client_id_required");
    }

    [Fact]
    public void Update_ValidCommand_Passes()
    {
        // Arrange
        var command = ClientTestData.CreateValidUpdateCommand(Guid.NewGuid());

        // Act
        var result = new UpdateClientCommandValidator(_clock).Validate(command);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void List_SearchOverMaximum_ReturnsStableCode()
    {
        // Arrange
        var query = new ListClientsQuery(Search: new string('a', 256));

        // Act
        var result = new ListClientsQueryValidator().Validate(query);

        // Assert
        Assert.Contains(result.Errors, e =>
            e.PropertyName == "Search" && e.ErrorCode == "client_search_too_long");
    }

    [Fact]
    public void List_UnknownActivity_ReturnsStableCode()
    {
        // Arrange
        var query = new ListClientsQuery(Search: null, Activity: (ClientActivityFilter)999);

        // Act
        var result = new ListClientsQueryValidator().Validate(query);

        // Assert
        Assert.Contains(result.Errors, e =>
            e.PropertyName == "Activity" && e.ErrorCode == "client_activity_filter_invalid");
    }
}
