using System.Reflection;
using System.Text.RegularExpressions;
using Infrastructure.Email;

namespace Infrastructure.IntegrationTests.Email;

/// <summary>
/// Regras que a auditoria de email do 8B (guia 07, secção 6) fixou para os 13 templates:
/// Outlook (cabeçalho e botão), contraste, modo escuro, preheader, rodapé de marca e
/// tratamento por "tu" com a grafia do Acordo Ortográfico.
/// </summary>
public sealed partial class EmailTemplateGuardTests
{
    private const string Prefix = "Infrastructure.Email.Templates.";

    private static readonly Assembly InfrastructureAssembly = typeof(EmailButtonMarkup).Assembly;

    public static TheoryData<string> Templates()
    {
        var data = new TheoryData<string>();
        foreach (var name in InfrastructureAssembly.GetManifestResourceNames()
                     .Where(name => name.StartsWith(Prefix, StringComparison.Ordinal) && name.EndsWith(".html", StringComparison.Ordinal))
                     .Order(StringComparer.Ordinal))
            data.Add(name[Prefix.Length..]);
        return data;
    }

    [Fact]
    public void AllThirteenTemplatesAreEmbedded() => Assert.Equal(13, Templates().Count);

    [Theory]
    [MemberData(nameof(Templates))]
    public void Template_HasOutlookFallbacks(string template)
    {
        var html = Read(template);

        Assert.Contains("class=\"ptm-header\" bgcolor=\"#1a1a1a\" style=\"background-color:#1a1a1a;", html, StringComparison.Ordinal);
        Assert.Contains("xmlns:v=\"urn:schemas-microsoft-com:vml\"", html, StringComparison.Ordinal);
        // O Outlook ignora padding numa <table>.
        Assert.DoesNotMatch(TablePadding(), html);

        var links = ButtonLinks().Matches(html).Count;
        var vml = VmlButtons().Matches(html).Count;
        Assert.Equal(links, vml);
    }

    [Theory]
    [MemberData(nameof(Templates))]
    public void Template_HasDarkModeMobileAndPreheader(string template)
    {
        var html = Read(template);

        Assert.Contains("<meta name=\"color-scheme\" content=\"light dark\">", html, StringComparison.Ordinal);
        Assert.Contains("@media (prefers-color-scheme: dark)", html, StringComparison.Ordinal);
        Assert.Contains("@media (max-width:480px)", html, StringComparison.Ordinal);
        Assert.Contains("mso-hide:all", html, StringComparison.Ordinal);
        Assert.Contains("{{preheader}}", html, StringComparison.Ordinal);
        // No escuro o texto fica claro: uma caixa clara sem a classe ficaria ilegível.
        Assert.DoesNotMatch(LightBoxWithoutDarkClass(), html);
    }

    [Theory]
    [MemberData(nameof(Templates))]
    public void Template_UsesAccessibleColoursAndBrandFooter(string template)
    {
        var html = Read(template);

        // Azul antigo (~2,7:1 com branco) e cinzentos claros sobre branco falhavam WCAG AA.
        Assert.DoesNotContain("#00a8e8", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("#999999", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("#aaaaaa", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("#777777", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("class=\"ptm-footer\"", html, StringComparison.Ordinal);
        Assert.Contains("Por favor, não respondas a este email.", html, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Templates))]
    public void Template_UsesTuAndCurrentSpelling(string template)
    {
        var text = VisibleText(Read(template));

        Assert.DoesNotMatch(FormalAddress(), text);
        Assert.DoesNotMatch(OldSpelling(), text);
        Assert.DoesNotContain("password", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("—", text, StringComparison.Ordinal);
        Assert.DoesNotContain("&mdash;", text, StringComparison.Ordinal);
    }

    [Fact]
    public void SessionReminderEmojis_AreHiddenFromScreenReaders()
    {
        var html = Read("Notifications.session-reminder.html");

        Assert.Equal(3, AriaHiddenEmoji().Matches(html).Count);
    }

    [Fact]
    public void AccountAlreadyExists_ShowsTheCopyableSignInLink()
    {
        var html = Read("Auth.account-already-exists.html");

        Assert.Contains("<span class=\"ptm-link\" style=\"color:#0369a1;word-break:break-all\">{{action_url}}</span>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void TemplatesUseLfLineEndingsOnly()
    {
        foreach (var template in Templates())
            Assert.DoesNotContain("\r", Read(template), StringComparison.Ordinal);
    }

    private static string Read(string template)
    {
        using var stream = InfrastructureAssembly.GetManifestResourceStream(Prefix + template)
            ?? throw new InvalidOperationException($"Template {template} not embedded.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>Texto que o leitor vê: sem etiquetas, sem estilos e sem comentários condicionais.</summary>
    private static string VisibleText(string html) =>
        Tags().Replace(StyleBlocks().Replace(html, " "), " ");

    [GeneratedRegex(@"<table[^>]*style=""[^""]*padding:", RegexOptions.CultureInvariant)]
    private static partial Regex TablePadding();

    [GeneratedRegex(@"(?<!class=""ptm-callout"" )style=""background-color:#(f0f9ff|fff8e1|ecfdf5|f3f4f6|fef2f2)", RegexOptions.CultureInvariant)]
    private static partial Regex LightBoxWithoutDarkClass();

    [GeneratedRegex(@"<v:roundrect xmlns:v=""urn:schemas-microsoft-com:vml""[^>]* href=""\{\{[a-z_]+\}\}""[^>]*fillcolor=""#0369a1"">", RegexOptions.CultureInvariant)]
    private static partial Regex VmlButtons();

    [GeneratedRegex(@"<a href=""\{\{[a-z_]+\}\}""\s+style=""display:inline-block", RegexOptions.CultureInvariant)]
    private static partial Regex ButtonLinks();

    [GeneratedRegex(@"\b(você|vocês|seu|sua|seus|suas|responda|pediu|terá|pode ignorar|tem uma|lembra)\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex FormalAddress();

    [GeneratedRegex(@"\b(actualiz\w*|activ\w*|reactiv\w*|factur\w*)\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex OldSpelling();

    [GeneratedRegex(@"<span aria-hidden=""true"" style=""[^""]*"">&#\d+;</span>", RegexOptions.CultureInvariant)]
    private static partial Regex AriaHiddenEmoji();

    [GeneratedRegex(@"<style[\s\S]*?</style>|<!--[\s\S]*?-->", RegexOptions.CultureInvariant)]
    private static partial Regex StyleBlocks();

    [GeneratedRegex(@"<[^>]+>", RegexOptions.CultureInvariant)]
    private static partial Regex Tags();
}
