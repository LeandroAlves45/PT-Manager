using System.Text.Json;
using System.Text.Json.Serialization;

namespace Application.Features.Training.ExerciseVideos;

/// <summary>
/// Contrato persistido dos durable jobs de vídeo: tipos, versão, chaves de
/// idempotência e payloads fechados.
/// </summary>
public static class ExerciseVideoJobs
{
    public const string ProcessType = "exercise_video.process";
    public const string ExpireType = "exercise_video.expire";
    public const string DeleteObjectType = "exercise_video.delete_object";
    public const int Version = 1;

    private static readonly JsonSerializerOptions PayloadOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static string ProcessIdempotencyKey(Guid videoId) => $"{ProcessType}:{videoId:N}";
    public static string ExpireIdempotencyKey(Guid videoId) => $"{ExpireType}:{videoId:N}";
    public static string DeleteObjectIdempotencyKey(Guid videoId) => $"{DeleteObjectType}:{videoId:N}";

    /// <summary>Payload de process e expire.</summary>
    public static string SerializeVideoPayload(Guid videoId) =>
        JsonSerializer.Serialize(new VideoPayload(videoId), PayloadOptions);

    /// <summary>Payload de eliminação: o objeto é validado contra o id derivado.</summary>
    public static string SerializeDeleteObjectPayload(Guid videoId, string objectKey) =>
        JsonSerializer.Serialize(new DeleteObjectPayload(videoId, objectKey), PayloadOptions);

    internal static bool TryReadVideoPayload(string payload, out Guid videoId)
    {
        videoId = Guid.Empty;
        var parsed = Deserialize<VideoPayload>(payload);
        if (parsed is null || parsed.VideoId == Guid.Empty)
            return false;

        videoId = parsed.VideoId;
        return true;
    }

    internal static bool TryReadDeleteObjectPayload(
        string payload,
        out Guid videoId,
        out string objectKey)
    {
        videoId = Guid.Empty;
        objectKey = string.Empty;
        var parsed = Deserialize<DeleteObjectPayload>(payload);
        if (parsed is null || parsed.VideoId == Guid.Empty ||
            string.IsNullOrWhiteSpace(parsed.ObjectKey))
            return false;

        videoId = parsed.VideoId;
        objectKey = parsed.ObjectKey;
        return true;
    }

    private static T? Deserialize<T>(string payload) where T : class
    {
        if (string.IsNullOrWhiteSpace(payload))
            return null;

        try
        {
            return JsonSerializer.Deserialize<T>(payload, PayloadOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record VideoPayload(
        [property: JsonPropertyName("video_id")] Guid VideoId);

    private sealed record DeleteObjectPayload(
        [property: JsonPropertyName("video_id")] Guid VideoId,
        [property: JsonPropertyName("object_key")] string ObjectKey);
}
