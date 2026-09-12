namespace Application.Common.Abstractions;

/// <summary>
/// Contéudo já validado, descodificado, reencodado e (quando o perfil o exige)
/// moderado, pronto a publicar.
/// </summary>
public sealed record MediaUploadRequest(
    ReadOnlyMemory<byte> Content,
    string ContentType,
    MediaAssetKind Kind,
    Guid TrainerId
);
