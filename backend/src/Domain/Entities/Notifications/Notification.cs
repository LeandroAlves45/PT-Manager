using Domain.Exceptions;
using Domain.ValueObjects;

namespace Domain.Entities.Notifications;

/// <summary>
/// Notificação por email a um destinatário do tenant, com template e dados,
/// estado de entrega e histórico de tentativas.
/// </summary>
/// <remarks>
/// Estados válidos (CHECK do schema): 'pending', 'sent', 'failed', 'bounced'.
/// Transições permitidas:
///   pending → sent      (entrega confirmada)
///   pending → failed    (tentativa falhou; pode voltar a pending num retry)
///   failed  → pending   (retry agendado pelo job handler, Sprint 5)
///   sent    → bounced   (bounce assíncrono reportado pelo provider)
/// A entidade rejeita qualquer outra transição.
/// </remarks>
public class Notification
{
    public Guid Id { get; private set; }
    public Guid OwnerTrainerId { get; private set; }
    /// <summary>
    /// Cliente relacionado -> null para notificações para personal trainer.
    /// </summary>
    public Guid? ClientId { get; private set; }
    public string RecipientEmail { get; private set; } = null!;
    public string NotificationType { get; private set; } = null!;
    public string TemplateKey { get; private set; } = null!;
    /// <summary>
    /// Dados do template em JSON.
    /// </summary>
    public string? TemplateData { get; private set; }
    public string Status { get; private set; } = null!;
    public int RetryCount { get; private set; }
    public DateTime? LastRetryAt { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTime? SentAt { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private Notification() { }

    /// <summary>Cria uma notificação em estado de "pending", pronta para o job de envio.</summary>
    public Notification(
        Guid ownerTrainerId,
        Guid? clientId,
        EmailAddress recipientEmail,
        string notificationType,
        string templateKey,
        string? templateDataJson,
        DateTime now
    )
    {
        if (string.IsNullOrWhiteSpace(notificationType))
            throw new DomainException("Type of notification cannot be empty.");
        if (notificationType.Length > 50)
            throw new DomainException("Type of notification cannot exceed 50 characters.");
        if (string.IsNullOrWhiteSpace(templateKey))
            throw new DomainException("Template is required.");
        if (templateKey.Length > 255)
            throw new DomainException("Template key cannot exceed 255 characters.");

        Id = Guid.NewGuid();
        OwnerTrainerId = ownerTrainerId;
        ClientId = clientId;
        RecipientEmail = recipientEmail.Value;
        NotificationType = notificationType;
        TemplateKey = templateKey;
        TemplateData = templateDataJson;
        Status = "pending";
        RetryCount = 0;
        IsDeleted = false;
        CreatedAt = now;
        UpdatedAt = now;
    }

    /// <summary>Marca a notificação como entregue com sucesso.</summary>
    public void MarkSent(DateTime now)
    {
        EnsureNotDeleted();
        if (Status != "pending")
            throw new DomainException("Only one pending notification can be marked as sent.");

        Status = "sent";
        SentAt = now;
        UpdatedAt = now;
    }

    /// <summary>Regista uma tentativa falhada com a mensagem de erro sanitizada.</summary>
    public void MarkFailed(string sanitizedError, DateTime now)
    {
        EnsureNotDeleted();
        if (Status != "pending")
            throw new DomainException("Only one pending notification can be marked as failed.");

        Status = "failed";
        RetryCount += 1;
        LastRetryAt = now;
        ErrorMessage = sanitizedError;
        UpdatedAt = now;
    }

    /// <summary>Recoloca uma notificação falhada em fila para retry.</summary>
    public void RequeueForRetry(DateTime now)
    {
        EnsureNotDeleted();
        if (Status != "failed")
            throw new DomainException("Only one failed notification can be requeued for retry.");

        Status = "pending";
        UpdatedAt = now;
    }

    /// <summary>Regista um bounce assíncrono reportado pelo provider.</summary>
    public void MarkBounced(DateTime now)
    {
        EnsureNotDeleted();
        if (Status != "sent")
            throw new DomainException("Only one sent notification can be marked as bounced.");

        Status = "bounced";
        UpdatedAt = now;
    }

    /// <summary>Soft delete da notificação.</summary>
    public void SoftDelete(DateTime now)
    {
        if (Status == "sent")
            throw new DomainException("Cannot delete a sent notification.");

        IsDeleted = true;
        UpdatedAt = now;
    }

    private void EnsureNotDeleted()
    {
        if (IsDeleted)
            throw new DomainException("Cannot modify a deleted notification.");
    }
}
