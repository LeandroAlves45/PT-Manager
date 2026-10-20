using Domain.Entities.Billing;
using Domain.Exceptions;

namespace Domain.UnitTests.Entities.Billing;

/// <summary>Pagamento de subscrição contado no painel do superuser (RN-018).</summary>
public sealed class PlatformSubscriptionPaymentTests
{
    private static readonly DateTime Now = new(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Constructor_NormalizesCurrencyAndKeepsTheNetAmount()
    {
        var payment = new PlatformSubscriptionPayment(
            Guid.NewGuid(), " in_123 ", 2900, 2789, "EUR", Now, Now);

        Assert.Equal("in_123", payment.StripeInvoiceId);
        Assert.Equal("eur", payment.Currency);
        Assert.Equal(2789, payment.NetCents);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-1, -1)]
    [InlineData(100, 101)]
    public void Constructor_RejectsZeroNegativeOrNetAboveAmount(long amount, long net)
    {
        var action = () => new PlatformSubscriptionPayment(
            Guid.NewGuid(), "in_1", amount, net, "eur", Now, Now);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void Constructor_RejectsANonUtcPaymentTime()
    {
        var action = () => new PlatformSubscriptionPayment(
            Guid.NewGuid(), "in_1", 100, 90, "eur", DateTime.SpecifyKind(Now, DateTimeKind.Local), Now);

        Assert.Throws<DomainException>(action);
    }
}
