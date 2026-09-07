namespace Application.Features.Jobs.Dispatching;

/// <summary>Ativa uma passagem limitada pelos durables jobs e pela outbox.</summary>
public interface IJobDispatchActivation
{
    Task ActivateAsync(CancellationToken cancellationToken);
}
