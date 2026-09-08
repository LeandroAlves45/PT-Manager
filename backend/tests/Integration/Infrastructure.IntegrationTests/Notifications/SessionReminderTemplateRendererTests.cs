using Application.Features.Notifications.Delivery;
using Infrastructure.Email;

namespace Infrastructure.IntegrationTests.Notifications;

/// <summary>
/// Verifica o renderer do único template de negócio.
/// Os nomes chegam de dados do utilizador.
/// O HTML e o texto simples são construídos por caminhos independentes, e o texto
/// nunca é derivado por remoção de tags: uma remoção ingénua deixaria passar
/// exactamente o conteúdo que o encoding pretende neutralizar.
/// </summary>
public sealed class SessionReminderTemplateRendererTests
{
    // 15:30 UTC em Julho corresponde a 16:30 em Lisboa (WEST, UTC+1).
    private static readonly DateTimeOffset SummerUtc =
        new(2026, 7, 15, 15, 30, 0, TimeSpan.Zero);

    // 15:30 UTC em Janeiro corresponde a 15:30 em Lisboa (WET, UTC+0).
    private static readonly DateTimeOffset WinterUtc =
        new(2026, 1, 15, 15, 30, 0, TimeSpan.Zero);

    [Fact]
    public void Render_ConvertsUtcToTrainerTimezoneDuringDaylightSaving()
    {
        var rendered = SessionReminderTemplateRenderer.Render(
            CreateData(startsAt: SummerUtc, timezone: "Europe/Lisbon"));

        Assert.Contains("16:30", rendered.Html, StringComparison.Ordinal);
        Assert.Contains("16:30", rendered.Text, StringComparison.Ordinal);
        Assert.Contains("15/07/2026", rendered.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_ConvertsUtcToTrainerTimezoneOutsideDaylightSaving()
    {
        var rendered = SessionReminderTemplateRenderer.Render(
            CreateData(startsAt: WinterUtc, timezone: "Europe/Lisbon"));

        Assert.Contains("15:30", rendered.Html, StringComparison.Ordinal);
        Assert.Contains("15/01/2026", rendered.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_UsesTrainerTimezoneNotServerTimezone()
    {
        var lisbon = SessionReminderTemplateRenderer.Render(
            CreateData(startsAt: SummerUtc, timezone: "Europe/Lisbon"));
        var saoPaulo = SessionReminderTemplateRenderer.Render(
            CreateData(startsAt: SummerUtc, timezone: "America/Sao_Paulo"));

        Assert.NotEqual(lisbon.Text, saoPaulo.Text);
        Assert.Contains("12:30", saoPaulo.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_WhenTimezoneIsUnknown_ThrowsTimeZoneNotFound()
    {
        // Cair silenciosamente no fuso do servidor produziria uma hora errada e
        // plausível, que é pior do que uma falha explícita.
        Assert.Throws<TimeZoneNotFoundException>(() =>
            SessionReminderTemplateRenderer.Render(
                CreateData(timezone: "Mars/Olympus_Mons")));
    }

    [Fact]
    public void Render_EncodesHtmlInDynamicValues()
    {
        var rendered = SessionReminderTemplateRenderer.Render(CreateData(
            clientName: "<script>alert('x')</script>",
            trainerName: "Bob & Alice"));

        Assert.DoesNotContain("<script>", rendered.Html, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", rendered.Html, StringComparison.Ordinal);
        Assert.Contains("Bob &amp; Alice", rendered.Html, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_PlainTextKeepsOriginalCharactersAndHasNoMarkup()
    {
        var rendered = SessionReminderTemplateRenderer.Render(CreateData(
            clientName: "Bob & Alice",
            trainerName: "João"));

        // O texto simples não passa por HTML encoding nem por remoção de tags:
        // é escrito de raiz, por isso mantém os caracteres originais.
        Assert.Contains("Bob & Alice", rendered.Text, StringComparison.Ordinal);
        Assert.Contains("João", rendered.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("<", rendered.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("&amp;", rendered.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_ProducesStableSubjectAndNonEmptyBodies()
    {
        var rendered = SessionReminderTemplateRenderer.Render(CreateData());

        Assert.Equal("Lembrete da sua sessão", rendered.Subject);
        Assert.False(string.IsNullOrWhiteSpace(rendered.Html));
        Assert.False(string.IsNullOrWhiteSpace(rendered.Text));
    }

    [Fact]
    public void Render_LeavesNoUnresolvedPlaceholder()
    {
        var rendered = SessionReminderTemplateRenderer.Render(CreateData());

        Assert.DoesNotContain("{{", rendered.Html, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_HtmlComesFromTheEmbeddedTemplate()
    {
        var rendered = SessionReminderTemplateRenderer.Render(CreateData());

        // Prova que o corpo é o recurso embebido e não uma string construída no
        // código do gateway.
        Assert.Contains("<!doctype html>", rendered.Html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PT Manager", rendered.Html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Render_RejectsBlankClientName(string? clientName)
    {
        Assert.Throws<ArgumentException>(() =>
            SessionReminderTemplateRenderer.Render(CreateData(clientName: clientName!)));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Render_RejectsBlankTrainerName(string? trainerName)
    {
        Assert.Throws<ArgumentException>(() =>
            SessionReminderTemplateRenderer.Render(CreateData(trainerName: trainerName!)));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Render_RejectsBlankTimezone(string? timezone)
    {
        Assert.Throws<ArgumentException>(() =>
            SessionReminderTemplateRenderer.Render(CreateData(timezone: timezone!)));
    }

    [Fact]
    public void Render_RejectsNamesAboveMaximumLength()
    {
        Assert.Throws<ArgumentException>(() =>
            SessionReminderTemplateRenderer.Render(
                CreateData(clientName: new string('a', 201))));
    }

    [Fact]
    public void Render_AcceptsNameAtMaximumLength()
    {
        var rendered = SessionReminderTemplateRenderer.Render(
            CreateData(clientName: new string('a', 200)));

        Assert.NotNull(rendered);
    }

    [Fact]
    public void Render_RejectsNonUtcInstant()
    {
        // O contrato do template declara o instante como UTC; aceitar outro offset
        // permitiria dupla conversão.
        var data = CreateData() with
        {
            SessionStartsAtUtc = new DateTimeOffset(2026, 7, 15, 15, 30, 0, TimeSpan.FromHours(2))
        };

        Assert.Throws<ArgumentException>(() => SessionReminderTemplateRenderer.Render(data));
    }

    [Fact]
    public void Render_RejectsNullData()
    {
        Assert.Throws<ArgumentNullException>(
            () => SessionReminderTemplateRenderer.Render(null!));
    }

    private static SessionReminderTemplateData CreateData(
        string clientName = "Ana",
        string trainerName = "Rui",
        DateTimeOffset? startsAt = null,
        string timezone = "Europe/Lisbon") =>
        new(clientName, trainerName, startsAt ?? SummerUtc, timezone);
}
