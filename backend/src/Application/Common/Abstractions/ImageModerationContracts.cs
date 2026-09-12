namespace Application.Common.Abstractions;

/// <summary>Veredicto de moderação de um conteúdo visual.</summary>
public enum ImageModerationVerdict
{
    Approved,
    Rejected,
    ReviewRequired,
    Unavailable
}

/// <summary>Conteúdo submetido a moderação. São os bytes que serão publicados.</summary>
public sealed record ImageModerationRequest(
    ReadOnlyMemory<byte> Content,
    string ContentType);

/// <summary>Resultado da moderação.</summary>
public sealed record ImageModerationResult(
    ImageModerationVerdict Verdict,
    string? ReasonCode = null);
