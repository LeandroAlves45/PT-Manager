namespace Application.Common.Abstractions;

/// <summary>Referência de um asset já persistido no storage externo.</summary>
public sealed record StoredMedia(string Url, string PublicId);

/// <summary>Resultado de um upload. Media só é preenchido em sucesso.</summary>
public sealed record MediaUploadOutcome(
    MediaStorageStatus Status,
    StoredMedia? Media = null,
    string? FailureCode = null
);

/// <summary>Resultado de uma eliminação idempotente.</summary>
public sealed record MediaDeletionOutcome(
    MediaStorageStatus Status,
    string? FailureCode = null
);
