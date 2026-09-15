namespace Application.Features.Jobs.Dispatching;

/// <summary>Executa um tipo exato de mensagem de outbox.</summary>
public interface IOutboxMessageHandler
{
    string MessageType { get; }

    /// <summary>
    /// Quando false, a mensagem é entregue a trainers ativos com subscrição em
    /// qualquer estado. É necessário para avisos de falha de pagamento e de
    /// cancelamento e para limpeza de media, que ocorrem precisamente quando a
    /// subscrição deixou de estar ativa.
    /// </summary>
    bool RequiresActiveSubscription => true;

    Task<DispatchItemOutcome> HandleAsync(
        OutboxMessageEnvelope message,
        CancellationToken cancellationToken
    );
}
