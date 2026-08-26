namespace Application.Features.Billing.Abstractions;

/// <summary>Porta externa  restrita ao Customer Portal.</summary>
public interface ICustomerPortalGateway
{
    Task<Uri> CreateCustomerPortalAsync(
        CreateCustomerPortalRequest request,
        CancellationToken cancellationToken
    );
}
