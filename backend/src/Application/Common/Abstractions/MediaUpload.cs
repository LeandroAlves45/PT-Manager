namespace Application.Common.Abstractions;

/// <summary>Contéudo a enviar para o storage externo.</summary>
public sealed record MediaUpload(
    Stream Content,
    string ContentType,
    long LengthInBytes
);
