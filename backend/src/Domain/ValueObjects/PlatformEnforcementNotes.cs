using Domain.Exceptions;

namespace Domain.ValueObjects;

/// <summary>Regra da nota livre que o superuser junta ao motivo de um bloqueio.</summary>
public static class PlatformEnforcementNotes
{
    /// <summary>Comprimento máximo da nota.</summary>
    public const int MaxLength = 500;

    /// <summary>Normaliza a nota: vazia passa a null; acima do máximo é recusada.</summary>
    public static string? Normalize(string? note)
    {
        var normalized = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (normalized?.Length > MaxLength)
            throw new DomainException($"Platform enforcement note cannot exceed {MaxLength} characters.");
        return normalized;
    }
}
