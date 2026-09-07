namespace Application.Features.Jobs.Dispatching;

/// <summary>Executa um tipo exato de mensagem de outbox.</summary>
public interface IOutboxMessageHandler
{
    string MessageType { get; }

    Task<DispatchItemOutcome> HandleAsync(
        OutboxMessageEnvelope message,
        CancellationToken cancellationToken
    );
}
