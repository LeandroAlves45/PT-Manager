using System.Text.Json;
using System.Text.Json.Serialization;
using Application.Common.Abstractions;
using Application.Features.Jobs.Dispatching;

namespace Infrastructure.Jobs;

/// <summary>Elimina, depois do commit, o asset de imagem que deixou de estar referenciado.</summary>
internal abstract class MediaDeletionOutboxHandler : IOutboxMessageHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    private readonly IMediaStorage _storage;

    protected MediaDeletionOutboxHandler(IMediaStorage storage) =>
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));

    public abstract string MessageType { get; }

    public async Task<DispatchItemOutcome> HandleAsync(
        OutboxMessageEnvelope message,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (message.TrainerId is not { } trainerId ||
            trainerId == Guid.Empty)
            return DispatchItemOutcome.PermanentFailure("media_deletion_tenant_missing");

        DeletionPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<DeletionPayload>(message.Payload, JsonOptions);
        }
        catch (JsonException)
        {
            return DispatchItemOutcome.PermanentFailure("media_deletion_payload_invalid");
        }

        if (string.IsNullOrWhiteSpace(payload?.PublicId))
            return DispatchItemOutcome.PermanentFailure("media_deletion_payload_invalid");

        var outcome = await _storage.DeleteAsync(payload.PublicId, trainerId, cancellationToken);

        return outcome.Status switch
        {
            MediaStorageStatus.Success => DispatchItemOutcome.Succeeded(),

            // Desligado depois de ter havido uploads: repetir é o comportamento
            // certo. Esgotadas as tentativas, a mensagem vai para dead letter,
            // que é o sinal operacional de assets por eliminar.
            MediaStorageStatus.Disabled or MediaStorageStatus.TransientFailure or
                MediaStorageStatus.InvalidResponse =>
                DispatchItemOutcome.TransientFailure(outcome.FailureCode ?? "media_deletion_transient"),

            _ => DispatchItemOutcome.PermanentFailure(outcome.FailureCode ?? "media_deletion_failed")
        };
    }

    private sealed record DeletionPayload(
        [property: JsonPropertyName("public_id")] string PublicId);
}

/// <summary>Consome <c>trainer-logo.delete</c>, produzido por <c>TrainerSettingsStore</c>.</summary>
internal sealed class TrainerLogoDeletionOutboxHandler : MediaDeletionOutboxHandler
{
    public TrainerLogoDeletionOutboxHandler(IMediaStorage storage)
        : base(storage)
    {
    }

    public override string MessageType => "trainer-logo.delete";
}

/// <summary>Consome <c>client-avatar.delete</c>, produzido por <c>MyAvatarStore</c>.</summary>
internal sealed class ClientAvatarDeletionOutboxHandler : MediaDeletionOutboxHandler
{
    public ClientAvatarDeletionOutboxHandler(IMediaStorage storage)
        : base(storage)
    {
    }

    public override string MessageType => "client-avatar.delete";
}
