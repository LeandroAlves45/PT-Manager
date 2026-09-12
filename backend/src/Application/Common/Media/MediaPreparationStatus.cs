namespace Application.Common.Media;

/// <summary>Resultado da preparação de uma imagem, antes de qualquer persistência</summary>
public enum MediaPreparationStatus
{
    Prepared,
    InvalidImage,
    Rejected,
    ReviewRequired,
    ModerationUnavailable,
    StorageDisabled,
    StorageFailed
}
