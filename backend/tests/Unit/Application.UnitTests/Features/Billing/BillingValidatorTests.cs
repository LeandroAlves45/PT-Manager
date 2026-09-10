using Application.Features.Billing.CreateCheckout;
using Application.Features.Billing.CreateCustomerPortal;

namespace Application.UnitTests.Features.Billing;

public sealed class BillingValidatorTests
{
    [Fact]
    public async Task Checkout_EmptyOperationId_ReturnsStableCode()
    {
        var result = await new CreateCheckoutCommandValidator().ValidateAsync(
            new CreateCheckoutCommand(Guid.Empty, "PRO"), TestContext.Current.CancellationToken);
        Assert.Contains(result.Errors, error => error.ErrorCode == "billing_operation_id_required");
    }

    [Theory]
    [InlineData("")]
    [InlineData("FREE")]
    [InlineData("starter")]
    [InlineData("ENTERPRISE")]
    public async Task Checkout_UnsupportedTier_ReturnsStableCode(string tier)
    {
        var result = await new CreateCheckoutCommandValidator().ValidateAsync(
            new CreateCheckoutCommand(Guid.NewGuid(), tier), TestContext.Current.CancellationToken);
        Assert.Contains(result.Errors, error => error.ErrorCode == "billing_tier_invalid");
    }

    [Theory]
    [InlineData("STARTER")]
    [InlineData("PRO")]
    public async Task Checkout_PublishedTier_IsAccepted(string tier)
    {
        var result = await new CreateCheckoutCommandValidator().ValidateAsync(
            new CreateCheckoutCommand(Guid.NewGuid(), tier), TestContext.Current.CancellationToken);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Commands_DoNotExposeCallerControlledRedirects()
    {
        var checkout = typeof(CreateCheckoutCommand).GetProperties().Select(property => property.Name);
        var portal = typeof(CreateCustomerPortalCommand).GetProperties().Select(property => property.Name);
        Assert.DoesNotContain("SuccessUrl", checkout);
        Assert.DoesNotContain("CancelUrl", checkout);
        Assert.DoesNotContain("ReturnUrl", portal);
    }
}
