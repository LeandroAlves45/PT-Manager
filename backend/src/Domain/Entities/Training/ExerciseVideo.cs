using Domain.Exceptions;
using Domain.ValueObjects;

namespace Domain.Entities.Training;

/// <summary>
/// Vídeo gerido de um exercício, guardado em storage privado e validado
/// de forma assíncrona.
/// </summary>
public sealed class ExerciseVideo
{
    /// <summary>Tipos de conteúdo aceites na autorização de upload.</summary>
    public static readonly IReadOnlySet<string> AcceptedContentTypes =
        new HashSet<string>(StringComparer.Ordinal) { "video/mp4", "video/quicktime" };

    private const string ObjectKeyRoot = "exercise-videos";
    private const int MaxContentTypeLength = 50;
    private const int MaxFailureCodeLength = 100;
    private const int MaxETagLength = 128;
    private const int MaxCodecLength = 8;

    public Guid Id { get; private set; }
    public Guid ExerciseId { get; private set; }
    public Guid? OwnerTrainerId { get; private set; }
    public ExerciseVideoStatus Status { get; private set; } = ExerciseVideoStatus.Pending;
    public string ObjectKey { get; private set; } = null!;
    public string ContentType { get; private set; } = null!;
    public long DeclaredSizeBytes { get; private set; }
    public long? StoredSizeBytes { get; private set; }
    public string? StoredETag { get; private set; }
    public long? DurationMilliseconds { get; private set; }
    public int? Width { get; private set; }
    public int? Height { get; private set; }
    public string? VideoCodec { get; private set; }
    public string? AudioCodec { get; private set; }
    public string? FailureCode { get; private set; }
    public DateTime UploadExpiresAt { get; private set; }
    public DateTime? ProcessingStartedAt { get; private set; }
    public DateTime? ReadyAt { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private ExerciseVideo() { }

    /// <summary>Cria um vídeo pendente com identificador de storage gerado pelo servidor.</summary>
    public ExerciseVideo(
        Guid exerciseId,
        Guid? ownerTrainerId,
        string contentType,
        long declaredSizeBytes,
        Guid createdByUserId,
        DateTime uploadExpiresAt,
        DateTime now)
    {
        if (exerciseId == Guid.Empty)
            throw new DomainException("Exercise ID is required.");
        if (ownerTrainerId.HasValue && ownerTrainerId.Value == Guid.Empty)
            throw new DomainException("Owner trainer ID cannot be empty.");
        if (createdByUserId == Guid.Empty)
            throw new DomainException("Creator user ID is required.");
        if (contentType is null || !AcceptedContentTypes.Contains(contentType) ||
            contentType.Length > MaxContentTypeLength)
            throw new DomainException("Exercise video content type is not accepted.");
        if (declaredSizeBytes <= 0)
            throw new DomainException("Exercise video declared size must be positive.");
        if (uploadExpiresAt <= now)
            throw new DomainException("Upload authorization must expire in the future.");

        Id = Guid.NewGuid();
        ExerciseId = exerciseId;
        OwnerTrainerId = ownerTrainerId;
        Status = ExerciseVideoStatus.Pending;
        ObjectKey = BuildObjectKey(ownerTrainerId, Id);
        ContentType = contentType;
        DeclaredSizeBytes = declaredSizeBytes;
        CreatedByUserId = createdByUserId;
        UploadExpiresAt = uploadExpiresAt;
        CreatedAt = now;
        UpdatedAt = now;
    }

    /// <summary>Indica se a janela de upload ainda está aberta no instante indicado.</summary>
    public bool IsUploadWindowOpen(DateTime now) => now <= UploadExpiresAt;

    /// <summary>Regista a confirmação do upload no fornecedor e passa a Processing.</summary>
    public void MarkUploaded(long storedSizeBytes, string storedETag, DateTime now)
    {
        EnsureTransition(ExerciseVideoStatus.Processing);

        if (!IsUploadWindowOpen(now))
            throw new DomainException("The upload window is closed.");
        if (storedSizeBytes != DeclaredSizeBytes)
            throw new DomainException("Stored size does not match the declared size.");
        if (string.IsNullOrWhiteSpace(storedETag) || storedETag.Length > MaxETagLength)
            throw new DomainException("Stored ETag is invalid.");

        StoredSizeBytes = storedSizeBytes;
        StoredETag = storedETag;
        ProcessingStartedAt = now;
        Status = ExerciseVideoStatus.Processing;
        UpdatedAt = now;
    }

    public void MarkReady(
        long durationMilliseconds,
        int width,
        int height,
        string videoCodec,
        string? audioCodec,
        DateTime now)
    {
        EnsureTransition(ExerciseVideoStatus.Ready);

        if (durationMilliseconds <= 0)
            throw new DomainException("Exercise video duration must be positive.");
        if (width <= 0 || height <= 0)
            throw new DomainException("Exercise video dimensions must be positive.");
        if (!IsValidCodec(videoCodec))
            throw new DomainException("Exercise video codec is invalid.");
        if (audioCodec is not null && !IsValidCodec(audioCodec))
            throw new DomainException("Exercise audio codec is invalid.");

        DurationMilliseconds = durationMilliseconds;
        Width = width;
        Height = height;
        VideoCodec = videoCodec;
        AudioCodec = audioCodec;
        ReadyAt = now;
        Status = ExerciseVideoStatus.Ready;
        UpdatedAt = now;
    }

    public void Reject(string failureCode, DateTime now) =>
        Terminate(ExerciseVideoStatus.Rejected, failureCode, now);

    public void Fail(string failureCode, DateTime now) =>
        Terminate(ExerciseVideoStatus.Failed, failureCode, now);

    /// <summary>
    /// Deriva o identificador do objeto a partir de dados confiáveis. O mesmo
    /// cálculo é usado para validar o identificador antes de qualquer eliminação.
    /// </summary>
    public static string BuildObjectKey(Guid? ownerTrainerId, Guid videoId)
    {
        if (videoId == Guid.Empty)
            throw new DomainException("Video ID is required.");
        if (ownerTrainerId.HasValue && ownerTrainerId.Value == Guid.Empty)
            throw new DomainException("Owner trainer ID cannot be empty.");

        return ownerTrainerId.HasValue
            ? $"{ObjectKeyRoot}/trainers/{ownerTrainerId.Value:N}/{videoId:N}"
            : $"{ObjectKeyRoot}/global/{videoId:N}";
    }

    /// <summary>
    /// Indica se o identificador pertence ao espaço do owner indicado. Um
    /// identificador de outro tenant, global ou malformado nunca é aceite.
    /// </summary>
    public static bool IsObjectKeyOwnedBy(string? objectKey, Guid? ownerTrainerId)
    {
        if (string.IsNullOrWhiteSpace(objectKey))
            return false;

        var prefix = ownerTrainerId.HasValue
            ? $"{ObjectKeyRoot}/trainers/{ownerTrainerId.Value:N}/"
            : $"{ObjectKeyRoot}/global/";

        if (!objectKey.StartsWith(prefix, StringComparison.Ordinal))
            return false;

        var leaf = objectKey[prefix.Length..];
        return leaf.Length == 32 && Guid.TryParseExact(leaf, "N", out _);
    }

    private void Terminate(ExerciseVideoStatus next, string failureCode, DateTime now)
    {
        EnsureTransition(next);

        if (!IsValidFailureCode(failureCode))
            throw new DomainException("Failure code is invalid.");

        FailureCode = failureCode;
        Status = next;
        UpdatedAt = now;
    }

    private void EnsureTransition(ExerciseVideoStatus next)
    {
        if (!Status.CanTransitionTo(next))
            throw new DomainException(
                $"Exercise video cannot transition from {Status.Value} to {next.Value}.");
    }

    private static bool IsValidFailureCode(string? failureCode) =>
        !string.IsNullOrWhiteSpace(failureCode) &&
        failureCode.Length <= MaxFailureCodeLength &&
        failureCode.All(character =>
            character is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_');

    // Um fourcc é persistido só quando é ASCII imprimível e curto: nunca
    // transporta bytes arbitrários do ficheiro para a base de dados.
    private static bool IsValidCodec(string? codec) =>
        !string.IsNullOrWhiteSpace(codec) &&
        codec.Length <= MaxCodecLength &&
        codec.All(character => character is >= ' ' and <= '~');
}
