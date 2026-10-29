using System.Globalization;
using System.Text;

namespace Application.Features.Administration.CatalogImport;

/// <summary>Chave de comparação de nomes na importação do catálogo global.</summary>
public static class CatalogImportName
{
    /// <summary>
    /// Sem acentos, em minúsculas e com os espaços reduzidos a um só. "Feijão  Preto" e
    /// "feijao preto" dão a mesma chave e por isso contam como o mesmo item.
    /// </summary>
    public static string Key(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var decomposed = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var previousWasSpace = false;

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsWhiteSpace(character))
            {
                if (!previousWasSpace)
                    builder.Append(' ');
                previousWasSpace = true;
                continue;
            }

            builder.Append(char.ToLowerInvariant(character));
            previousWasSpace = false;
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
