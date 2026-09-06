using Application.Features.Authentication.Google;
using Google.Apis.Auth;
using Infrastructure.Identity;
using Microsoft.Extensions.Options;

namespace Infrastructure.IntegrationTests.Identity;

/// <summary>
/// Cobre o adapter Google sem rede e sem tokens reais. Fabricar assinaturas Google
/// não provaria nada sobre o código do projeto; o que se testa é o mapeamento do
/// payload, o nonce e o tratamento de falhas da biblioteca.
/// </summary>
public sealed class GoogleExternalIdentityVerifierTests
{
    [Fact]
    public async Task ValidWorkspacePayload_MapsAuthoritativeIdentityAndAudience()
    {
        var validator = new RecordingValidator(new GoogleJsonWebSignature.Payload
        {
            Subject = "subject-1",
            Email = "trainer@example.com",
            EmailVerified = true,
            HostedDomain = "example.com",
            Name = "Trainer",
            Nonce = "nonce"
        });
        var verifier = Create(validator);

        var result = await verifier.VerifyAsync(
            "google", "opaque-id-token", "nonce",
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsEmailAuthoritative);
        Assert.Equal("subject-1", result.Value.Subject);
        Assert.Equal("trainer@example.com", result.Value.Email);
        Assert.Equal("Trainer", result.Value.FullName);
        Assert.Contains("client.apps.googleusercontent.com", validator.Audience!);
    }

    [Fact]
    public async Task GmailPayloadWithoutHostedDomain_IsAuthoritative()
    {
        var result = await Create(new RecordingValidator(Payload("trainer@gmail.com")))
            .VerifyAsync(
                "google", "opaque-id-token", "nonce",
                TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsEmailAuthoritative);
    }

    [Fact]
    public async Task ExternalDomainWithoutHostedDomain_IsNotAuthoritative()
    {
        // Um domínio externo sem `hd` não prova posse do endereço: o trainer fica pendente.
        var result = await Create(new RecordingValidator(Payload("trainer@empresa.pt")))
            .VerifyAsync(
                "google", "opaque-id-token", "nonce",
                TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.IsEmailAuthoritative);
    }

    [Fact]
    public async Task NonceMismatch_ReturnsInvalidCredential()
    {
        var payload = Payload("trainer@gmail.com");
        payload.Nonce = "different";

        var result = await Create(new RecordingValidator(payload)).VerifyAsync(
            "google", "opaque-id-token", "nonce",
            TestContext.Current.CancellationToken);

        Assert.Equal(GoogleAuthenticationErrors.InvalidCredential.Code, result.Error!.Code);
    }

    [Fact]
    public async Task MissingNonceInPayload_ReturnsInvalidCredential()
    {
        var payload = Payload("trainer@gmail.com");
        payload.Nonce = null;

        var result = await Create(new RecordingValidator(payload)).VerifyAsync(
            "google", "opaque-id-token", "nonce",
            TestContext.Current.CancellationToken);

        Assert.Equal(GoogleAuthenticationErrors.InvalidCredential.Code, result.Error!.Code);
    }

    [Fact]
    public async Task UnverifiedEmail_ReturnsForbiddenOutcome()
    {
        var payload = Payload("trainer@gmail.com");
        payload.EmailVerified = false;

        var result = await Create(new RecordingValidator(payload)).VerifyAsync(
            "google", "opaque-id-token", "nonce",
            TestContext.Current.CancellationToken);

        Assert.Equal(GoogleAuthenticationErrors.EmailNotVerified.Code, result.Error!.Code);
    }

    [Fact]
    public async Task MissingSubject_ReturnsInvalidCredential()
    {
        var payload = Payload("trainer@gmail.com");
        payload.Subject = null;

        var result = await Create(new RecordingValidator(payload)).VerifyAsync(
            "google", "opaque-id-token", "nonce",
            TestContext.Current.CancellationToken);

        Assert.Equal(GoogleAuthenticationErrors.InvalidCredential.Code, result.Error!.Code);
    }

    [Fact]
    public async Task MalformedEmail_ReturnsInvalidCredential()
    {
        var result = await Create(new RecordingValidator(Payload("not-an-email")))
            .VerifyAsync(
                "google", "opaque-id-token", "nonce",
                TestContext.Current.CancellationToken);

        Assert.Equal(GoogleAuthenticationErrors.InvalidCredential.Code, result.Error!.Code);
    }

    [Fact]
    public async Task UnsupportedProvider_IsRejectedBeforeValidation()
    {
        var validator = new RecordingValidator(Payload("trainer@gmail.com"));

        var result = await Create(validator).VerifyAsync(
            "facebook", "opaque-id-token", "nonce",
            TestContext.Current.CancellationToken);

        Assert.Equal(GoogleAuthenticationErrors.InvalidCredential.Code, result.Error!.Code);
        Assert.Equal(0, validator.Calls);
    }

    [Fact]
    public async Task InvalidJwt_ReturnsInvalidCredentialWithoutLeakingDetail()
    {
        var result = await Create(new ThrowingValidator(new InvalidJwtException("bad")))
            .VerifyAsync(
                "google", "opaque-id-token", "nonce",
                TestContext.Current.CancellationToken);

        Assert.Equal(GoogleAuthenticationErrors.InvalidCredential.Code, result.Error!.Code);
        Assert.DoesNotContain("bad", result.Error.Description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MalformedTokenThroughRealWrapper_ReturnsInvalidCredential()
    {
        // Percorre o wrapper real: um token que nem sequer é um JWT nunca chega à rede.
        var verifier = Create(new GoogleIdTokenValidator());

        var result = await verifier.VerifyAsync(
            "google", "definitely-not-a-jwt", "nonce",
            TestContext.Current.CancellationToken);

        Assert.Equal(GoogleAuthenticationErrors.InvalidCredential.Code, result.Error!.Code);
    }

    [Fact]
    public async Task TransportFailure_ReturnsProviderUnavailable()
    {
        var result = await Create(new ThrowingValidator(new HttpRequestException("down")))
            .VerifyAsync(
                "google", "opaque-id-token", "nonce",
                TestContext.Current.CancellationToken);

        Assert.Equal(GoogleAuthenticationErrors.ProviderUnavailable.Code, result.Error!.Code);
    }

    private static GoogleJsonWebSignature.Payload Payload(string email) => new()
    {
        Subject = "subject-1",
        Email = email,
        EmailVerified = true,
        Name = "Trainer",
        Nonce = "nonce"
    };

    private static GoogleExternalIdentityVerifier Create(IGoogleIdTokenValidator validator) =>
        new(Options.Create(new GoogleOptions
        {
            ClientId = "client.apps.googleusercontent.com"
        }), validator);

    private sealed class RecordingValidator(GoogleJsonWebSignature.Payload payload) :
        IGoogleIdTokenValidator
    {
        internal IEnumerable<string>? Audience { get; private set; }
        internal int Calls { get; private set; }

        public Task<GoogleJsonWebSignature.Payload> ValidateAsync(
            string idToken,
            GoogleJsonWebSignature.ValidationSettings settings)
        {
            Calls++;
            Audience = settings.Audience?.Cast<string>();
            return Task.FromResult(payload);
        }
    }

    private sealed class ThrowingValidator(Exception exception) : IGoogleIdTokenValidator
    {
        public Task<GoogleJsonWebSignature.Payload> ValidateAsync(
            string idToken,
            GoogleJsonWebSignature.ValidationSettings settings) =>
            Task.FromException<GoogleJsonWebSignature.Payload>(exception);
    }
}
