using Application.Errors;

namespace Application.Features.Administration.CatalogImport;

/// <summary>Códigos estáveis dos erros da importação CSV do catálogo global.</summary>
public static class CatalogImportErrors
{
    public const string FileTooLarge = "catalog_import_file_too_large";
    public const string FileEmpty = "catalog_import_file_empty";
    public const string EncodingInvalid = "catalog_import_encoding_invalid";
    public const string QuoteUnterminated = "catalog_import_quote_unterminated";
    public const string ColumnMissing = "catalog_import_column_missing";
    public const string ColumnUnknown = "catalog_import_column_unknown";
    public const string ColumnDuplicate = "catalog_import_column_duplicate";
    public const string TooManyRows = "catalog_import_too_many_rows";
    public const string RowColumns = "catalog_import_row_columns";
    public const string NumberInvalid = "catalog_import_number_invalid";
    public const string NumberRequired = "catalog_import_number_required";
    public const string NameFormula = "catalog_import_name_formula";
    public const string DifficultyInvalid = "catalog_import_difficulty_invalid";

    /// <summary>
    /// Erro de uma linha do ficheiro. O campo fica <c>Rows[linha].coluna</c>, com o nome da
    /// coluna tal como aparece no cabeçalho do CSV, ou só <c>Rows[linha]</c> quando o erro
    /// é da linha inteira.
    /// </summary>
    public static ValidationError Row(int line, string? column, string code, string message) =>
        new(column is null ? $"Rows[{line}]" : $"Rows[{line}].{column}", code, message);
}
