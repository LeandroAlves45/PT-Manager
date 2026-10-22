using Application.Features.ClientPortal.UpdateMyProfile;
using Application.Features.Clients.CreateClient;
using Application.Features.Clients.UpdateClient;

namespace Application.UnitTests.Features.Clients;

/// <summary>
/// O email de contacto é validado com a mesma regra do value object <c>EmailAddress</c>: o que
/// o domínio recusaria com uma exceção (500) é recusado antes, com um código estável (400).
/// </summary>
public sealed class ContactEmailValidationTests
{
    private readonly StubClock _clock = new StubClock { UtcNow = ClientTestData.NowUtc };

    public static TheoryData<string> EmailsRejectedByTheDomain => new()
    {
        "a@b",
        "a b@c.pt",
        "a@c.pt.",
        "a@.c.pt",
        "a@b@c.pt",
        "sem-arroba"
    };

    [Theory]
    [MemberData(nameof(EmailsRejectedByTheDomain))]
    public void CreateClient_EmailRejectedByDomain_ReturnsStableCode(string email)
    {
        var command = ClientTestData.CreateValidCommand() with { ContactEmail = email };

        var result = new CreateClientCommandValidator(_clock).Validate(command);

        Assert.Contains(result.Errors, error =>
            error.PropertyName == "ContactEmail" && error.ErrorCode == "client_email_invalid");
    }

    [Theory]
    [MemberData(nameof(EmailsRejectedByTheDomain))]
    public void UpdateClient_EmailRejectedByDomain_ReturnsStableCode(string email)
    {
        var command = ClientTestData.CreateValidUpdateCommand(Guid.NewGuid()) with
        {
            ContactEmail = email
        };

        var result = new UpdateClientCommandValidator(_clock).Validate(command);

        Assert.Contains(result.Errors, error =>
            error.PropertyName == "ContactEmail" && error.ErrorCode == "client_email_invalid");
    }

    // O perfil do portal já não recebe email: o cliente muda o nome e os contactos, nunca o
    // email da ficha.
    [Theory]
    [InlineData("Joana Silva")]
    [InlineData("  Joana  ")]
    public void UpdateMyProfile_ValidName_Passes(string name)
    {
        var command = new UpdateMyProfileCommand(name, "+351911111111", null, null);

        var result = new UpdateMyProfileCommandValidator().Validate(command);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateMyProfile_EmptyName_IsRejected(string name)
    {
        var command = new UpdateMyProfileCommand(name, "+351911111111", null, null);

        var result = new UpdateMyProfileCommandValidator().Validate(command);

        var error = Assert.Single(result.Errors);
        Assert.Equal("client_name_invalid", error.ErrorCode);
    }

    [Fact]
    public void UpdateMyProfile_NameLongerThanLimit_IsRejected()
    {
        var command = new UpdateMyProfileCommand(new string('a', 256), "+351911111111", null, null);

        var result = new UpdateMyProfileCommandValidator().Validate(command);

        var error = Assert.Single(result.Errors);
        Assert.Equal("client_name_invalid", error.ErrorCode);
    }

    [Fact]
    public void UpdateMyProfile_NameWithNullCharacter_IsRejected()
    {
        var command = new UpdateMyProfileCommand("Ana\u0000", "+351911111111", null, null);

        var result = new UpdateMyProfileCommandValidator().Validate(command);

        var error = Assert.Single(result.Errors);
        Assert.Equal("text_invalid_character", error.ErrorCode);
    }
}
