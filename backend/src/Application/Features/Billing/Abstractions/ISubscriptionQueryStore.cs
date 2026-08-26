using Application.Features.Billing.Dtos;

namespace Application.Features.Billing.Abstractions;

/// <summary>Consulta a subscription dentro do tenant efetivo.</summary>
public interface ISubscriptionQueryStore
{
    Task<SubscriptionDto?> GetSubscriptionAsync(
        Guid trainerId,
        CancellationToken cancellationToken
    );
}
