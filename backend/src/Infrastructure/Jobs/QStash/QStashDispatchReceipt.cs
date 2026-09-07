namespace Infrastructure.Jobs.QStash;

/// <summary>Recibo técnico de uma ativação QStash autenticado.</summary>
internal sealed record QStashDispatchReceipt
{
    private QStashDispatchReceipt()
    {
    }

    /// <summary>Cria um recibo sem persistir o identificador original.</summary>
    public QStashDispatchReceipt(
        string jtiHash,
        DateTime tokenExpiresAt,
        DateTime consumedAt)
    {
        if (string.IsNullOrWhiteSpace(jtiHash) ||
            jtiHash.Length != 64 ||
            !jtiHash.All(Uri.IsHexDigit))
            throw new ArgumentException(
                "JTI hash must be a SHA-256 hexadecimal value.",
                nameof(jtiHash));

        if (tokenExpiresAt.Kind != DateTimeKind.Utc || consumedAt.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Receipt timestamps must be UTC.");

        JtiHash = jtiHash.ToLowerInvariant();
        TokenExpiresAt = tokenExpiresAt;
        ConsumedAt = consumedAt;
    }

    public string JtiHash { get; private set; } = null!;
    public DateTime TokenExpiresAt { get; private set; }
    public DateTime ConsumedAt { get; private set; }
}
