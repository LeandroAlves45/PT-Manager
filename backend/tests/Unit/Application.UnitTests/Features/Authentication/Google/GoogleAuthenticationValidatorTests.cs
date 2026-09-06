using Application.Features.Authentication.Google.Link;
using Application.Features.Authentication.Google.SignIn;
using Xunit;

namespace Application.UnitTests.Features.Authentication.Google;

/// <summary>
/// Limites sintáticos aplicados antes de qualquer verificação criptográfica: um ID token
/// gigante nunca deve chegar à biblioteca do Google nem à base de dados.
/// </summary>
public sealed class GoogleAuthenticationValidatorTests
{
    private readonly GoogleSignInCommandValidator _signIn = new();
    private readonly GoogleLinkCommandValidator _link = new();

    [Fact]
    public async Task SignIn_ValidCommand_Passes()
    {
        var result = await _signIn.ValidateAsync(
            new GoogleSignInCommand("id-token", "nonce", null),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task SignIn_EmptyIdToken_Fails()
    {
        var result = await _signIn.ValidateAsync(
            new GoogleSignInCommand(string.Empty, "nonce", null),
            TestContext.Current.CancellationToken);

        Assert.Contains(result.Errors, error =>
            error.ErrorCode == "google_sign_in_id_token_required");
    }

    [Fact]
    public async Task SignIn_EmptyNonce_Fails()
    {
        // Um pedido sem cookie de challenge chega ao handler com nonce vazio.
        var result = await _signIn.ValidateAsync(
            new GoogleSignInCommand("id-token", string.Empty, null),
            TestContext.Current.CancellationToken);

        Assert.Contains(result.Errors, error =>
            error.ErrorCode == "google_sign_in_nonce_required");
    }

    [Fact]
    public async Task SignIn_OversizedIdToken_Fails()
    {
        var result = await _signIn.ValidateAsync(
            new GoogleSignInCommand(new string('t', 10_001), "nonce", null),
            TestContext.Current.CancellationToken);

        Assert.Contains(result.Errors, error =>
            error.ErrorCode == "google_sign_in_id_token_too_long");
    }

    [Fact]
    public async Task SignIn_OversizedInvitationToken_Fails()
    {
        var result = await _signIn.ValidateAsync(
            new GoogleSignInCommand("id-token", "nonce", new string('i', 513)),
            TestContext.Current.CancellationToken);

        Assert.Contains(result.Errors, error =>
            error.ErrorCode == "google_sign_in_invitation_token_too_long");
    }

    [Fact]
    public async Task Link_ValidCommand_Passes()
    {
        var result = await _link.ValidateAsync(
            new GoogleLinkCommand("id-token", "nonce", "Password1!"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Link_EmptyCurrentPassword_Fails()
    {
        var result = await _link.ValidateAsync(
            new GoogleLinkCommand("id-token", "nonce", string.Empty),
            TestContext.Current.CancellationToken);

        Assert.Contains(result.Errors, error =>
            error.ErrorCode == "google_link_current_password_required");
    }

    [Fact]
    public async Task Link_OversizedNonce_Fails()
    {
        var result = await _link.ValidateAsync(
            new GoogleLinkCommand("id-token", new string('n', 513), "Password1!"),
            TestContext.Current.CancellationToken);

        Assert.Contains(result.Errors, error =>
            error.ErrorCode == "google_link_nonce_too_long");
    }
}
