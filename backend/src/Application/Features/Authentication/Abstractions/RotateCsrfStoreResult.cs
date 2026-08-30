namespace Application.Features.Authentication.Abstractions;

/// <summary>Resultado do bootstrapping de CSRF sem expor o hash persistido.</summary>
public sealed record RotateCsrfStoreResult(
    RotateCsrfStoreStatus Kind,
    string? RawCsrfToken
)
{
    public static RotateCsrfStoreResult Rotated(string rawCsrfToken)
    {
        if (string.IsNullOrWhiteSpace(rawCsrfToken))
            throw new ArgumentException("Raw CSRF token is required.", nameof(rawCsrfToken));

        return new(RotateCsrfStoreStatus.Rotated, rawCsrfToken);
    }

    public static RotateCsrfStoreResult Failure(RotateCsrfStoreStatus status)
    {
        if (status == RotateCsrfStoreStatus.Rotated)
            throw new ArgumentOutOfRangeException(nameof(status));

        return new(status, null);
    }
}
