namespace Application.Common.Abstractions;

/// <summary>
/// Estado técnico de uma operação sobre o storage de media. Existe para que o
/// chamador distinga indisponibilidade configurada de falha transitória e de
/// falha permanente, sem inspecionar exceções do fornecedor.
/// </summary>
public enum MediaStorageStatus
{
    Success,
    Disabled,
    InvalidResponse,
    TransientFailure,
    PermanentFailure
}
