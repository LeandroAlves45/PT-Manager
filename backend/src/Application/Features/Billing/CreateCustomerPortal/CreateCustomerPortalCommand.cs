namespace Application.Features.Billing.CreateCustomerPortal;

/// <summary>Entrada local para abrir o portal do cliente.</summary>
public sealed record CreateCustomerPortalCommand(
    Guid OperationId,
    Uri ReturnUrl
);
