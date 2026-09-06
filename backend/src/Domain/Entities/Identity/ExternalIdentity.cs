using Domain.Exceptions;

namespace Domain.Entities.Identity;

/// <summary>Associa um utilizador a uma identidade autenticada por um fornecedor externo.</summary>
public sealed class ExternalIdentity
{
    public const string GoogleProvider = "google";

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Provider { get; private set; } = null!;
    public string Subject { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }

    private ExternalIdentity() { }

    public ExternalIdentity(Guid userId, string provider, string subject, DateTime now)
    {
        if (userId == Guid.Empty)
            throw new DomainException("Extenal identity user is required");

        var normalizedProvider = provider?.Trim().ToLowerInvariant();
        if (normalizedProvider != GoogleProvider)
            throw new DomainException("External identity provider is invalid");

        var normalizedSubject = subject?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedSubject) || normalizedSubject.Length > 255)
            throw new DomainException("External identity subject is invalid");

        Id = Guid.NewGuid();
        UserId = userId;
        Provider = normalizedProvider;
        Subject = normalizedSubject;
        CreatedAt = RequireUtc(now);
    }

    private static DateTime RequireUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc
            ? value
            : throw new DomainException("External Identity timestamp must be UTC");
}
