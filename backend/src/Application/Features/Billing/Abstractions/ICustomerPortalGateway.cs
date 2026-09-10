namespace Application.Features.Billing.Abstractions;

/// <summary>Porta externa  restrita ao Customer Portal.</summary>
public interface ICustomerPortalGateway
{
    Task<CustomerPortalOutcome> CreateAsync(
        CreateCustomerPortalRequest request,
        CancellationToken cancellationToken
    );
}
