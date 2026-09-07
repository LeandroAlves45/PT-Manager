namespace Application.Features.Jobs.Dispatching;

/// <summary>Executa um tipo e versão exactos de durable job.</summary>
public interface IDurableJobHandler
{
    string JobType { get; }
    int JobVersion { get; }

    Task<DispatchItemOutcome> HandleAsync(
        DurableJobEnvelope job,
        CancellationToken cancellationToken
    );
}
