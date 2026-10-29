using System.Globalization;
using Application.Errors;
using FluentValidation.Results;

namespace Application.Features.Administration.CatalogImport;

/// <summary>Conversões e regras de célula partilhadas pelas importações de alimentos e exercícios.</summary>
public static class CatalogImportValues
{
    private static readonly char[] FormulaPrefixes = ['=', '+', '-', '@'];

    /// <summary>Texto opcional: vazio passa a <c>null</c>.</summary>
    public static string? Optional(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// Número com vírgula ou ponto decimal ("25,5" e "25.5"). Vazio devolve <c>null</c>.
    /// Separadores de milhares não são aceites: "1.000,5" é ambíguo e dá erro.
    /// </summary>
    public static bool TryParseDecimal(string value, out decimal? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(value))
            return true;

        var trimmed = value.Trim();
        if (trimmed.Contains(',') && trimmed.Contains('.'))
            return false;

        if (!decimal.TryParse(
                trimmed.Replace(',', '.'),
                NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out var parsed))
            return false;

        result = parsed;
        return true;
    }

    /// <summary>
    /// Verdadeiro quando o texto começa como uma fórmula de folha de cálculo. Um nome
    /// destes, copiado mais tarde para o Excel, pode correr como fórmula.
    /// </summary>
    public static bool LooksLikeFormula(string value)
    {
        var trimmed = value.TrimStart();
        return trimmed.Length > 0 && Array.IndexOf(FormulaPrefixes, trimmed[0]) >= 0;
    }

    /// <summary>
    /// Fica com a primeira linha de cada nome e devolve as seguintes como saltadas, para o
    /// mesmo ficheiro não criar dois itens iguais.
    /// </summary>
    public static (List<T> Unique, List<CatalogImportSkippedRowDto> Skipped) SplitInFileDuplicates<T>(
        IEnumerable<T> items,
        Func<T, int> line,
        Func<T, string> name)
    {
        ArgumentNullException.ThrowIfNull(items);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var unique = new List<T>();
        var skipped = new List<CatalogImportSkippedRowDto>();

        foreach (var item in items)
        {
            if (seen.Add(CatalogImportName.Key(name(item))))
                unique.Add(item);
            else
                skipped.Add(new CatalogImportSkippedRowDto(
                    line(item), name(item), CatalogImportSkipReasons.DuplicateInFile));
        }

        return (unique, skipped);
    }

    /// <summary>
    /// Converte as falhas do validador de criação em erros da linha, trocando o nome da
    /// propriedade (<c>Protein</c>) pela coluna do CSV (<c>proteina</c>).
    /// </summary>
    public static IEnumerable<ValidationError> ToRowErrors(
        int line,
        ValidationResult validation,
        IReadOnlyDictionary<string, string> columnByProperty)
    {
        ArgumentNullException.ThrowIfNull(validation);
        ArgumentNullException.ThrowIfNull(columnByProperty);

        return validation.Errors.Select(failure => CatalogImportErrors.Row(
            line,
            columnByProperty.TryGetValue(failure.PropertyName ?? string.Empty, out var column)
                ? column
                : string.IsNullOrWhiteSpace(failure.PropertyName) ? null : failure.PropertyName,
            failure.ErrorCode,
            failure.ErrorMessage));
    }
}
