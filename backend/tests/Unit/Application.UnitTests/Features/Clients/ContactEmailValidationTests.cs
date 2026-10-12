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
    public void UpdateMyProfile_EmailRejectedByDomain_ReturnsStableCode(string email)
    {
        var command = new UpdateMyProfileCommand(email, "+351911111111", null, null);

        var result = new UpdateMyProfileCommandValidator().Validate(command);

        Assert.Contains(result.Errors, error =>
            error.PropertyName == "ContactEmail" && error.ErrorCode == "client_email_invalid");
    }

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

    [Theory]
    [InlineData("joao@ptmanager.local")]
    [InlineData("  joao@ptmanager.local  ")]
    [InlineData(null)]
    [InlineData("   ")]
    public void UpdateMyProfile_AcceptedOrEmptyEmail_Passes(string? email)
    {
        var command = new UpdateMyProfileCommand(email, "+351911111111", null, null);

        var result = new UpdateMyProfileCommandValidator().Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void UpdateMyProfile_EmailLongerThanLimit_KeepsTheLengthCode()
    {
        var email = new string('a', 251) + "@c.pt";
        var command = new UpdateMyProfileCommand(email, "+351911111111", null, null);

        var result = new UpdateMyProfileCommandValidator().Validate(command);

        var error = Assert.Single(result.Errors);
        Assert.Equal("client_email_too_long", error.ErrorCode);
    }
}
