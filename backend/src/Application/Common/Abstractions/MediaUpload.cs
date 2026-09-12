namespace Application.Common.Abstractions;

/// <summary>Conteúdo bruto e não confiável recebido na fronteira HTTP.</summary>
public sealed record MediaUpload(
    Stream Content,
    string ContentType,
    long LengthInBytes
);
