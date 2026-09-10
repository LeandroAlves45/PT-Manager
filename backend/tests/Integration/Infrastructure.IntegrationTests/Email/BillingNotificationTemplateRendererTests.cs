using System.Net;
using Infrastructure.Email;

namespace Infrastructure.IntegrationTests.Email;

public sealed class BillingNotificationTemplateRendererTests
{
    private static readonly Uri ManagementUrl =
        new("https://billing.example.test/manage");

    public static IEnumerable<object[]> SupportedKinds =>
    [
        [BillingNotificationTemplateRenderer.PaymentFailedKind],
        [BillingNotificationTemplateRenderer.TrialWillEndKind],
        [BillingNotificationTemplateRenderer.SubscriptionActivatedKind],
        [BillingNotificationTemplateRenderer.PaymentSucceededKind],
        [BillingNotificationTemplateRenderer.SubscriptionCanceledKind]
    ];

    [Theory]
    [MemberData(nameof(SupportedKinds))]
    public void Render_ForSupportedKind_ProducesHtmlAndTextWithEncodedUrl(string kind)
    {
        var rendered = BillingNotificationTemplateRenderer.Render(kind, ManagementUrl);

        Assert.False(string.IsNullOrWhiteSpace(rendered.Subject));
        Assert.Contains("linear-gradient(135deg,#1a1a1a 0%,#2d2d2d 100%)", rendered.Html, StringComparison.Ordinal);
        Assert.Contains("#00a8e8", rendered.Html, StringComparison.Ordinal);
        Assert.Contains("PT Manager", rendered.Html, StringComparison.Ordinal);
        Assert.StartsWith("<!doctype html>", rendered.Html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(ManagementUrl.AbsoluteUri, rendered.Html, StringComparison.Ordinal);
        Assert.Contains(ManagementUrl.AbsoluteUri, rendered.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("{{", rendered.Html, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_PaymentFailed_EncodesMaliciousUrl()
    {
        var maliciousUrl = new Uri(
            "https://billing.example.test/manage?x=%22%3E%3Cscript%3Ealert(1)%3C/script%3E");

        var rendered = BillingNotificationTemplateRenderer.Render(
            BillingNotificationTemplateRenderer.PaymentFailedKind,
            maliciousUrl);

        Assert.DoesNotContain("<script>", rendered.Html, StringComparison.Ordinal);
        Assert.Contains(WebUtility.HtmlEncode(maliciousUrl.AbsoluteUri), rendered.Html, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_WithHttpManagementUrl_FailsClosed()
    {
        var action = () => BillingNotificationTemplateRenderer.Render(
            BillingNotificationTemplateRenderer.PaymentFailedKind,
            new Uri("http://billing.example.test/manage"));

        Assert.Throws<ArgumentException>(action);
    }

    [Fact]
    public void Render_WithUnknownKind_FailsClosed()
    {
        var action = () => BillingNotificationTemplateRenderer.Render(
            "unknown_kind",
            ManagementUrl);

        Assert.Throws<ArgumentOutOfRangeException>(action);
    }
}
