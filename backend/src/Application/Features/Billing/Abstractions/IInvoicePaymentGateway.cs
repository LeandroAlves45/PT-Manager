namespace Application.Features.Billing.Abstractions;

/// <summary>Lê no provider o valor líquido de uma fatura de subscrição já paga.</summary>
public interface IInvoicePaymentGateway
{
    Task<InvoicePaymentOutcome> GetPaidInvoiceAsync(
        string providerInvoiceId,
        CancellationToken cancellationToken
    );
}
