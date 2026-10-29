using Infrastructure.Email;

namespace Infrastructure.IntegrationTests.Notifications;

/// <summary>
/// Verifica os quatro emails do bloqueio de um personal trainer: o texto em português, que o
/// cliente nunca vê o motivo nem a nota, e que o texto livre do superuser vai codificado no HTML.
/// </summary>
public sealed class AccountAccessTemplateRendererTests
{
    private static readonly DateTime EndsAtLocal = new(2026, 10, 31, 14, 56, 0, DateTimeKind.Unspecified);

    [Fact]
    public void ClientBlocked_NamesTheTrainerWithTheContraction_AndHidesReasonAndNote()
    {
        var rendered = AccountAccessTemplateRenderer.Render(new AccountAccessEmail(
            AccountAccessTemplateRenderer.ClientBlockedKind,
            "Marta Figueiredo",
            "Rita Sousa",
            "dangerous_information",
            "Nota interna",
            EndsAtLocal,
            "Europe/Lisbon"));

        Assert.Contains(
            "A conta do teu personal trainer, Rita Sousa, está suspensa na plataforma.",
            rendered.Text,
            StringComparison.Ordinal);
        Assert.Contains("31/10/2026 às 14:56 (Europe/Lisbon)", rendered.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Informação perigosa", rendered.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Nota interna", rendered.Html, StringComparison.Ordinal);
    }

    [Fact]
    public void ClientRestored_WithoutTrainerName_StaysGrammatical()
    {
        var rendered = AccountAccessTemplateRenderer.Render(new AccountAccessEmail(
            AccountAccessTemplateRenderer.ClientRestoredKind, null, null, null, null, null, null));

        Assert.Contains(
            "O acesso ao portal do teu personal trainer voltou ao normal.",
            rendered.Text,
            StringComparison.Ordinal);
        Assert.StartsWith("Olá,", rendered.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void TrainerBlocked_ShowsReasonAndNote_AndEncodesTheNoteInHtml()
    {
        var rendered = AccountAccessTemplateRenderer.Render(new AccountAccessEmail(
            AccountAccessTemplateRenderer.TrainerBlockedKind,
            "Rita Sousa",
            null,
            "dangerous_information",
            "<script>alert(1)</script> {{content}}",
            EndsAtLocal,
            "Europe/Lisbon"));

        Assert.Contains("Motivo: Informação perigosa.", rendered.Text, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", rendered.Html, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>", rendered.Html, StringComparison.Ordinal);
        Assert.Contains("{{content}}", rendered.Html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(AccountAccessTemplateRenderer.TrainerRestoredKind)]
    [InlineData(AccountAccessTemplateRenderer.ClientRestoredKind)]
    [InlineData(AccountAccessTemplateRenderer.ClientBlockedKind)]
    public void WhoCanStillEnter_GetsTheSignInButtonAndLink(string kind)
    {
        var rendered = AccountAccessTemplateRenderer.Render(
            new AccountAccessEmail(kind, "Ana", "Rita", null, null, EndsAtLocal, "Europe/Lisbon"),
            LoginUrl);

        Assert.Contains("<v:roundrect", rendered.Html, StringComparison.Ordinal);
        Assert.Contains("href=\"https://app.ptmanager.test/entrar\"", rendered.Html, StringComparison.Ordinal);
        Assert.Contains("Entrar: https://app.ptmanager.test/entrar", rendered.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("{{", rendered.Html, StringComparison.Ordinal);
    }

    [Fact]
    public void BlockedTrainer_GetsNoSignInButton()
    {
        var rendered = AccountAccessTemplateRenderer.Render(
            new AccountAccessEmail(
                AccountAccessTemplateRenderer.TrainerBlockedKind, "Rita", null, "dangerous_information", null, null, null),
            LoginUrl);

        Assert.DoesNotContain("/entrar", rendered.Html, StringComparison.Ordinal);
        Assert.DoesNotContain("Entrar:", rendered.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void NoteWithAPlaceholder_StaysAsText()
    {
        var rendered = AccountAccessTemplateRenderer.Render(
            new AccountAccessEmail(
                AccountAccessTemplateRenderer.TrainerBlockedKind, "Rita", null, "dangerous_information", "{{action}} {{preheader}}", null, null),
            LoginUrl);

        Assert.Contains("Nota: {{action}} {{preheader}}", rendered.Html, StringComparison.Ordinal);
        Assert.DoesNotContain("<v:roundrect", rendered.Html, StringComparison.Ordinal);
    }

    private static readonly Uri LoginUrl = new("https://app.ptmanager.test/entrar");
}
