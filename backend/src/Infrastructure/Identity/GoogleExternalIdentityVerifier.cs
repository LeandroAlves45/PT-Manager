using System.Security.Cryptography;
using System.Text;
using Application.Features.Authentication.Google;
using Application.Features.Authentication.Google.Abstractions;
using Application.Results;
using Domain.Entities.Identity;
using Domain.ValueObjects;
using Google.Apis.Auth;
using Microsoft.Extensions.Options;

namespace Infrastructure.Identity;

/// <summary>Adapta o payload Google ao contrato provider-neutral da Application.</summary>
internal sealed class GoogleExternalIdentityVerifier : IExternalIdentityVerifier
{
    private readonly GoogleOptions _options;
    private readonly IGoogleIdTokenValidator _validator;

    public GoogleExternalIdentityVerifier(
        IOptions<GoogleOptions> options,
        IGoogleIdTokenValidator validator)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
    }

    public async Task<Result<VerifiedExternalIdentity>> VerifyAsync(
        string provider,
        string idToken,
        string expectedNonce,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(provider, ExternalIdentity.GoogleProvider, StringComparison.Ordinal))
            return Result<VerifiedExternalIdentity>.Failure(
                GoogleAuthenticationErrors.InvalidCredential);

        cancellationToken.ThrowIfCancellationRequested();

        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await _validator.ValidateAsync(
                idToken,
                new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = [_options.ClientId],
                    IssuedAtClockTolerance = TimeSpan.FromSeconds(30),
                    ExpirationTimeClockTolerance = TimeSpan.Zero
                });
        }
        catch (InvalidJwtException)
        {
            return Result<VerifiedExternalIdentity>.Failure(
                GoogleAuthenticationErrors.InvalidCredential);
        }
        catch (FormatException)
        {
            return Result<VerifiedExternalIdentity>.Failure(
                GoogleAuthenticationErrors.InvalidCredential);
        }
        catch (HttpRequestException)
        {
            return Result<VerifiedExternalIdentity>.Failure(
                GoogleAuthenticationErrors.ProviderUnavailable);
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (!MatchesNonce(payload.Nonce, expectedNonce) ||
            string.IsNullOrWhiteSpace(payload.Subject) || payload.Subject.Length > 255 ||
            string.IsNullOrWhiteSpace(payload.Email))
            return Result<VerifiedExternalIdentity>.Failure(
                GoogleAuthenticationErrors.InvalidCredential);

        if (!payload.EmailVerified)
            return Result<VerifiedExternalIdentity>.Failure(
                GoogleAuthenticationErrors.EmailNotVerified);

        string email;
        try
        {
            email = new EmailAddress(payload.Email).Value;
        }
        catch (Domain.Exceptions.DomainException)
        {
            return Result<VerifiedExternalIdentity>.Failure(
                GoogleAuthenticationErrors.InvalidCredential);
        }

        var name = string.IsNullOrWhiteSpace(payload.Name) ? null : payload.Name.Trim();
        if (name?.Length > 255)
            name = name[..255];

        var authoritative = email.EndsWith("@gmail.com", StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrWhiteSpace(payload.HostedDomain);

        return Result<VerifiedExternalIdentity>.Success(new VerifiedExternalIdentity(
            ExternalIdentity.GoogleProvider,
            payload.Subject.Trim(),
            email,
            name,
            authoritative));
    }

    private static bool MatchesNonce(string? actual, string expected)
    {
        if (string.IsNullOrWhiteSpace(actual) || string.IsNullOrWhiteSpace(expected))
            return false;

        var actualBytes = Encoding.UTF8.GetBytes(actual);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        return actualBytes.Length == expectedBytes.Length &&
            CryptographicOperations.FixedTimeEquals(actualBytes, expectedBytes);
    }
}
