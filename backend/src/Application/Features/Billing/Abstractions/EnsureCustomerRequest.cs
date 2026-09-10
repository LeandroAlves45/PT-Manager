namespace Application.Features.Billing.Abstractions;

/// <summary>Dados mínimos para garantir um customer remoto.</summary>
public sealed record EnsureCustomerRequest(
    Guid TrainerId,
    string Email,
    string IdempotencyKey);
