using System.Data.Common;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Application.Common.Abstractions;
using Application.Features.Jobs.Dispatching;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure.Jobs.QStash;

/// <summary>Autentica pedidos QStash de acordo com o protocolo assinado.</summary>
internal sealed class QStashRequestAuthenticator : IInternalDispatchRequestAuthenticator
{
    private const string ExpectedIssuer = "Upstash";
    private const int MaximumSignatureLength = 8 * 1024;
    private const int MaximumJtiLength = 255;

    private readonly QStashOptions _options;
    private readonly IClock _clock;
    private readonly QStashReplayStore _replayStore;
    private readonly ILogger<QStashRequestAuthenticator> _logger;
    private readonly JsonWebTokenHandler _tokenHandler = new();

    public QStashRequestAuthenticator(
        IOptions<QStashOptions> options,
        IClock clock,
        QStashReplayStore replayStore,
        ILogger<QStashRequestAuthenticator> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _replayStore = replayStore ?? throw new ArgumentNullException(nameof(replayStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<InternalDispatchAuthenticationResult> AuthenticateAsync(
        string signature,
        ReadOnlyMemory<byte> rawBody,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
            return InternalDispatchAuthenticationResult.Unavailable();

        if (string.IsNullOrWhiteSpace(signature) ||
            signature.Length > MaximumSignatureLength ||
            rawBody.Length > _options.MaximumBodySize)
            return Reject();

        var token = await ValidateSignatureAsync(signature);
        if (token is null || !TryValidateClaims(token, rawBody.Span, out var claims))
            return Reject();

        var jtiHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(claims.Jti)))
            .ToLowerInvariant();

        try
        {
            var consumed = await _replayStore.TryConsumeAsync(
                jtiHash, claims.ExpiresAt, _clock.UtcNow, cancellationToken);

            return consumed
                ? InternalDispatchAuthenticationResult.Accepted()
                : InternalDispatchAuthenticationResult.Replay();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is DbException or RetryLimitExceededException or TimeoutException)
        {
            _logger.LogError(
                JobDispatchLogEvents.RequestRejected,
                "QStash replay persistence is unavailable with failure type {FailureType}.",
                exception.GetType().Name);

            return InternalDispatchAuthenticationResult.Unavailable();
        }
    }

    private async Task<JsonWebToken?> ValidateSignatureAsync(string signature)
    {
        try
        {
            var validation = await _tokenHandler.ValidateTokenAsync(
                signature,
                new TokenValidationParameters
                {
                    RequireSignedTokens = true,
                    RequireExpirationTime = false,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKeys =
                    [
                        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                            _options.CurrentSigningKey)),
                        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                            _options.NextSigningKey)),
                    ],
                    TryAllIssuerSigningKeys = true,
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ValidateIssuer = true,
                    ValidIssuer = ExpectedIssuer,
                    ValidateAudience = false,
                    ValidateLifetime = false,
                    ValidTypes = ["JWT"]
                });

            return validation.IsValid && validation.SecurityToken is JsonWebToken token
                ? token
                : null;
        }
        catch
        {
            // Todas as diferenças criptográficas têm a mesma resposta externa.
            return null;
        }
    }

    private bool TryValidateClaims(
        JsonWebToken token,
        ReadOnlySpan<byte> rawBody,
        out ValidatedQStashClaims claims)
    {
        claims = null!;

        if (!token.TryGetPayloadValue<string>(JwtRegisteredClaimNames.Sub, out var subject) ||
            !string.Equals(subject, _options.DestinationUrl!.AbsoluteUri, StringComparison.Ordinal) ||
            !token.TryGetPayloadValue<long>(JwtRegisteredClaimNames.Exp, out var exp) ||
            !token.TryGetPayloadValue<long>(JwtRegisteredClaimNames.Nbf, out var nbf) ||
            !token.TryGetPayloadValue<long>(JwtRegisteredClaimNames.Iat, out var iat) ||
            !token.TryGetPayloadValue<string>(JwtRegisteredClaimNames.Jti, out var jti) ||
            !token.TryGetPayloadValue<string>("body", out var bodyHash) ||
            string.IsNullOrWhiteSpace(jti) ||
            jti.Length > MaximumJtiLength)
            return false;

        var now = new DateTimeOffset(_clock.UtcNow).ToUnixTimeSeconds();
        var skew = checked((long)_options.ClockSkew.TotalSeconds);
        var maximumLifetime = checked((long)_options.MaximumTokenLifetime.TotalSeconds);

        if (exp <= now - skew ||
            nbf > now + skew ||
            iat > now + skew ||
            nbf > exp ||
            iat > exp ||
            exp - iat > maximumLifetime + skew)
            return false;

        var expectedBodyHash = Base64UrlEncoder.Encode(SHA256.HashData(rawBody));
        if (!FixedTimeEqualsWithoutPadding(bodyHash, expectedBodyHash))
            return false;

        claims = new ValidatedQStashClaims(
            jti,
            DateTimeOffset.FromUnixTimeSeconds(exp).UtcDateTime);

        return true;
    }

    private InternalDispatchAuthenticationResult Reject()
    {
        _logger.LogWarning(
            JobDispatchLogEvents.RequestRejected,
            "QStash dispatch request authentication was rejected.");

        return InternalDispatchAuthenticationResult.Invalid();
    }

    private static bool FixedTimeEqualsWithoutPadding(string left, string right)
    {
        var leftBytes = Encoding.ASCII.GetBytes(left.TrimEnd('='));
        var rightBytes = Encoding.ASCII.GetBytes(right.TrimEnd('='));
        return leftBytes.Length == rightBytes.Length &&
            CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    private sealed record ValidatedQStashClaims(string Jti, DateTime ExpiresAt);
}
