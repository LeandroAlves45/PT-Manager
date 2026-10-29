using System.Net;

namespace Infrastructure.Email;

/// <summary>
/// Botão de email gerado em código, igual ao dos templates: VML para o Outlook desktop (que
/// ignora o padding de um link) e um link normal para os outros clientes, mais o URL copiável.
/// </summary>
internal static class EmailButtonMarkup
{
    private const string Blue = "#0369a1";

    /// <summary>Devolve o bloco HTML do botão com o URL e o rótulo já codificados.</summary>
    internal static string Render(Uri url, string label)
    {
        ArgumentNullException.ThrowIfNull(url);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);

        var href = WebUtility.HtmlEncode(url.AbsoluteUri);
        var text = WebUtility.HtmlEncode(label);

        return $$"""
                          <table role="presentation" cellpadding="0" cellspacing="0" style="margin:10px auto 25px auto">
                            <tr>
                              <td align="center" bgcolor="{{Blue}}" style="background-color:{{Blue}};border-radius:8px">
                                <!--[if mso]>
                                <v:roundrect xmlns:v="urn:schemas-microsoft-com:vml" xmlns:w="urn:schemas-microsoft-com:office:word" href="{{href}}" style="height:52px;v-text-anchor:middle;width:280px" arcsize="15%" stroke="f" fillcolor="{{Blue}}">
                                  <w:anchorlock/>
                                  <center style="color:#ffffff;font-family:'Segoe UI',Arial,sans-serif;font-size:16px;font-weight:600">{{text}}</center>
                                </v:roundrect>
                                <![endif]-->
                                <!--[if !mso]><!-->
                                <a href="{{href}}"
                                  style="display:inline-block;padding:16px 40px;color:#ffffff;font-size:16px;font-weight:600;text-decoration:none;border-radius:8px">
                                  {{text}}
                                </a>
                                <!--<![endif]-->
                              </td>
                            </tr>
                          </table>
                          <p style="color:#666666;font-size:13px;text-align:center;margin:0;line-height:1.6">
                            Se o botão não funcionar, copia e cola este link no browser:<br>
                            <span class="ptm-link" style="color:{{Blue}};word-break:break-all">{{href}}</span>
                          </p>

            """;
    }
}
