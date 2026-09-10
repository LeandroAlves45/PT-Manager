using Infrastructure.Email;

namespace Infrastructure.IntegrationTests.Billing;

public sealed class BillingNotificationTemplateRendererTests
{
    [Theory]
    [InlineData("payment_failed", "Pagamento da subscrição requer atenção")]
    [InlineData("trial_will_end", "O período experimental termina em breve")]
    public void Render_SupportedKind_ProducesHtmlAndPlainTextWithCanonicalCta(
        string kind,
        string expectedSubject)
    {
        var url = new Uri("https://app.example.test/settings/billing?source=email&kind=test");

        var rendered = BillingNotificationTemplateRenderer.Render(kind, url);

        Assert.Equal(expectedSubject, rendered.Subject);
        Assert.Contains("https://app.example.test/settings/billing?source=email&amp;kind=test", rendered.Html);
        Assert.Contains(url.AbsoluteUri, rendered.Text);
        Assert.DoesNotContain("{{", rendered.Html, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_UnsupportedKind_FailsClosed()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BillingNotificationTemplateRenderer.Render(
                "unknown",
                new Uri("https://app.example.test/settings/billing")));
    }
}
