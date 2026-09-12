using System.Text.Json.Serialization;

namespace Infrastructure.Media.Moderation;

/// <summary>Corpo de <c>POST v1/images:annotate</c> com uma única imagem.</summary>
internal sealed record VisionAnnotateRequest(
    [property: JsonPropertyName("requests")] IReadOnlyList<VisionImageRequest> Requests);

internal sealed record VisionImageRequest(
    [property: JsonPropertyName("image")] VisionImage Image,
    [property: JsonPropertyName("features")] IReadOnlyList<VisionFeature> Features);

internal sealed record VisionImage(
    [property: JsonPropertyName("content")] string Content);

internal sealed record VisionFeature(
    [property: JsonPropertyName("type")] string Type);

/// <summary>Subconjunto da resposta necessário á decisão; o resto é ignorado.</summary>
internal sealed record VisionAnnotateResponse(
    [property: JsonPropertyName("responses")] IReadOnlyList<VisionImageResponse>? Responses);

internal sealed record VisionImageResponse(
    [property: JsonPropertyName("safeSearchAnnotation")] VisionSafeSearchAnnotation? SafeSearchAnnotation,
    [property: JsonPropertyName("error")] VisionStatus? Error);

internal sealed record VisionSafeSearchAnnotation(
    [property: JsonPropertyName("adult")] string? Adult,
    [property: JsonPropertyName("violence")] string? Violence,
    [property: JsonPropertyName("racy")] string? Racy);

internal sealed record VisionStatus(
    [property: JsonPropertyName("code")] int? Code,
    [property: JsonPropertyName("status")] string? Status);

/// <summary>Envelope de erro devolvido em respostas não 2xx.</summary>
internal sealed record VisionErrorEnvelope(
    [property: JsonPropertyName("error")] VisionStatus? Error);
