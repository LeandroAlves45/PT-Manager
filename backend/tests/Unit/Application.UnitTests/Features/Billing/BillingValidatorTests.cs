using Application.Features.Billing.CreateCheckout;
using Application.Features.Billing.CreateCustomerPortal;

namespace Application.UnitTests.Features.Billing;

public sealed class BillingValidatorTests
{
    private static readonly Uri HttpsUrl = new("https://app.example/billing");

    [Fact]
    public async Task Checkout_EmptyOperationId_ReturnsStableCode()
    {
        var command = ValidCheckoutCommand() with { OperationId = Guid.Empty };

        var result = await new CreateCheckoutCommandValidator().ValidateAsync(
            command,
            TestContext.Current.CancellationToken);

        Assert.Contains(result.Errors, error =>
            error.PropertyName == nameof(command.OperationId) &&
            error.ErrorCode == "billing_operation_id_required");
    }

    [Theory]
    [InlineData("")]
    [InlineData("FREE")]
    [InlineData("starter")]
    [InlineData("ENTERPRISE")]
    public async Task Checkout_UnsupportedTier_ReturnsStableCode(string tier)
    {
        var command = ValidCheckoutCommand() with { Tier = tier };

        var result = await new CreateCheckoutCommandValidator().ValidateAsync(
            command,
            TestContext.Current.CancellationToken);

        Assert.Contains(result.Errors, error =>
            error.PropertyName == nameof(command.Tier) &&
            error.ErrorCode == "billing_tier_invalid");
    }

    [Theory]
    [InlineData("STARTER")]
    [InlineData("PRO")]
    public async Task Checkout_PublishedTier_IsAccepted(string tier)
    {
        var command = ValidCheckoutCommand() with { Tier = tier };

        var result = await new CreateCheckoutCommandValidator().ValidateAsync(
            command,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Checkout_NonHttpsSuccessUrl_ReturnsStableCode()
    {
        var command = ValidCheckoutCommand() with
        {
            SuccessUrl = new Uri("http://app.example/success")
        };

        var result = await new CreateCheckoutCommandValidator().ValidateAsync(
            command,
            TestContext.Current.CancellationToken);

        Assert.Contains(result.Errors, error =>
            error.PropertyName == nameof(command.SuccessUrl) &&
            error.ErrorCode == "billing_success_url_invalid");
    }

    [Fact]
    public async Task Checkout_RelativeCancelUrl_ReturnsStableCode()
    {
        var command = ValidCheckoutCommand() with
        {
            CancelUrl = new Uri("/billing", UriKind.Relative)
        };

        var result = await new CreateCheckoutCommandValidator().ValidateAsync(
            command,
            TestContext.Current.CancellationToken);

        Assert.Contains(result.Errors, error =>
            error.PropertyName == nameof(command.CancelUrl) &&
            error.ErrorCode == "billing_cancel_url_invalid");
    }

    [Fact]
    public async Task Checkout_ValidCommand_IsAccepted()
    {
        var result = await new CreateCheckoutCommandValidator().ValidateAsync(
            ValidCheckoutCommand(),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Portal_EmptyOperationId_ReturnsStableCode()
    {
        var command = new CreateCustomerPortalCommand(Guid.Empty, HttpsUrl);

        var result = await new CreateCustomerPortalCommandValidator().ValidateAsync(
            command,
            TestContext.Current.CancellationToken);

        Assert.Contains(result.Errors, error =>
            error.PropertyName == nameof(command.OperationId) &&
            error.ErrorCode == "billing_operation_id_required");
    }

    [Fact]
    public async Task Portal_NonHttpsReturnUrl_ReturnsStableCode()
    {
        var command = new CreateCustomerPortalCommand(
            Guid.NewGuid(),
            new Uri("http://app.example/billing"));

        var result = await new CreateCustomerPortalCommandValidator().ValidateAsync(
            command,
            TestContext.Current.CancellationToken);

        Assert.Contains(result.Errors, error =>
            error.PropertyName == nameof(command.ReturnUrl) &&
            error.ErrorCode == "billing_return_url_invalid");
    }

    [Fact]
    public async Task Portal_RelativeReturnUrl_ReturnsStableCode()
    {
        var command = new CreateCustomerPortalCommand(
            Guid.NewGuid(),
            new Uri("/billing", UriKind.Relative));

        var result = await new CreateCustomerPortalCommandValidator().ValidateAsync(
            command,
            TestContext.Current.CancellationToken);

        Assert.Contains(result.Errors, error =>
            error.PropertyName == nameof(command.ReturnUrl) &&
            error.ErrorCode == "billing_return_url_invalid");
    }

    [Fact]
    public async Task Portal_ValidCommand_IsAccepted()
    {
        var result = await new CreateCustomerPortalCommandValidator().ValidateAsync(
            new CreateCustomerPortalCommand(Guid.NewGuid(), HttpsUrl),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
    }

    private static CreateCheckoutCommand ValidCheckoutCommand() => new(
        Guid.NewGuid(),
        "PRO",
        new Uri("https://app.example/success"),
        new Uri("https://app.example/cancel"));
}
