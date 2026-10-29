using System.Text;
using Application.Errors;
using Application.Results;

namespace Application.Features.Administration.CatalogImport;

/// <summary>Linha de dados de um CSV, com o número da linha onde começa no ficheiro.</summary>
public sealed record CatalogCsvRow(int Line, IReadOnlyList<string> Values);

/// <summary>CSV lido: colunas normalizadas do cabeçalho e linhas de dados não vazias.</summary>
public sealed class CatalogCsvDocument
{
    private readonly Dictionary<string, int> _columnIndex;

    internal CatalogCsvDocument(IReadOnlyList<string> columns, IReadOnlyList<CatalogCsvRow> rows)
    {
        Columns = columns;
        Rows = rows;
        _columnIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var index = 0; index < columns.Count; index++)
            _columnIndex.TryAdd(columns[index], index);
    }

    /// <summary>Nomes das colunas sem acentos, em minúsculas e sem espaços à volta.</summary>
    public IReadOnlyList<string> Columns { get; }

    /// <summary>Linhas de dados, sem o cabeçalho e sem linhas vazias.</summary>
    public IReadOnlyList<CatalogCsvRow> Rows { get; }

    /// <summary>Valor da coluna indicada, sem espaços à volta; vazio quando a coluna não existe.</summary>
    public string Get(CatalogCsvRow row, string column)
    {
        ArgumentNullException.ThrowIfNull(row);
        return _columnIndex.TryGetValue(column, out var index) && index < row.Values.Count
            ? row.Values[index].Trim()
            : string.Empty;
    }
}

/// <summary>
/// Lê o CSV de importação do catálogo global. Aceita o formato do Excel em português
/// (separador <c>;</c>) e o internacional (<c>,</c>), com aspas segundo a RFC 4180.
/// </summary>
/// <remarks>
/// O ficheiro é pequeno (limite no controller), por isso é lido de uma vez. Os erros de
/// ficheiro saem no campo <c>File</c> e os de linha em <c>Rows[n]</c>, com <c>n</c> igual
/// ao número da linha que o utilizador vê no editor.
/// </remarks>
public static class CatalogCsvReader
{
    /// <summary>Máximo de linhas de dados por importação.</summary>
    public const int MaxRows = 1000;

    /// <summary>Tamanho máximo do ficheiro aceite pela leitura.</summary>
    public const int MaxBytes = 1024 * 1024;

    /// <summary>Máximo de colunas no cabeçalho; os modelos têm no máximo 7.</summary>
    public const int MaxColumns = 32;

    private const string FileField = "File";

    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    /// <summary>
    /// Lê o ficheiro e confirma que o cabeçalho tem todas as colunas obrigatórias e
    /// nenhuma coluna desconhecida.
    /// </summary>
    public static async Task<Result<CatalogCsvDocument>> ReadAsync(
        Stream content,
        IReadOnlyCollection<string> requiredColumns,
        IReadOnlyCollection<string> optionalColumns,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(requiredColumns);
        ArgumentNullException.ThrowIfNull(optionalColumns);

        var bytes = await ReadBoundedAsync(content, cancellationToken);
        if (bytes is null)
            return FileFailure(CatalogImportErrors.FileTooLarge, "The file exceeds 1 MiB.");

        string text;
        try
        {
            text = StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return FileFailure(CatalogImportErrors.EncodingInvalid, "The file is not valid UTF-8.");
        }

        if (text.Length > 0 && text[0] == '﻿')
            text = text[1..];

        var parsed = Parse(text);
        if (parsed.UnterminatedQuoteLine is { } badLine)
            return Result<CatalogCsvDocument>.Failure(Error.Validation(
            [
                CatalogImportErrors.Row(
                    badLine, null, CatalogImportErrors.QuoteUnterminated, "A quoted value is not closed.")
            ]));

        if (parsed.TooManyRows)
            return FileFailure(CatalogImportErrors.TooManyRows, $"The file has more than {MaxRows} rows.");

        var records = parsed.Records;
        if (records.Count == 0)
            return FileFailure(CatalogImportErrors.FileEmpty, "The file has no header.");

        var header = records[0];
        // Um cabeçalho com milhares de colunas daria um erro por coluna e uma resposta enorme.
        if (header.Values.Count > MaxColumns)
            return FileFailure(CatalogImportErrors.ColumnUnknown, $"The header has more than {MaxColumns} columns.");

        var columns = header.Values.Select(CatalogImportName.Key).ToList();
        var errors = new List<ValidationError>();

        foreach (var duplicate in columns.Where(column => column.Length > 0)
                     .GroupBy(column => column).Where(group => group.Count() > 1))
            errors.Add(new ValidationError(
                FileField, CatalogImportErrors.ColumnDuplicate, $"Column '{duplicate.Key}' is repeated."));

        foreach (var required in requiredColumns.Where(required => !columns.Contains(required)))
            errors.Add(new ValidationError(
                FileField, CatalogImportErrors.ColumnMissing, $"Column '{required}' is missing."));

        foreach (var unknown in columns.Distinct().Where(column =>
                     !requiredColumns.Contains(column) && !optionalColumns.Contains(column)))
            errors.Add(new ValidationError(
                FileField, CatalogImportErrors.ColumnUnknown, $"Column '{unknown}' is not recognised."));

        if (errors.Count > 0)
            return Result<CatalogCsvDocument>.Failure(Error.Validation(errors));

        var rows = records.Skip(1)
            .Where(record => record.Values.Any(value => !string.IsNullOrWhiteSpace(value)))
            .ToList();

        if (rows.Count == 0)
            return FileFailure(CatalogImportErrors.FileEmpty, "The file has no data rows.");
        if (rows.Count > MaxRows)
            return FileFailure(CatalogImportErrors.TooManyRows, $"The file has more than {MaxRows} rows.");

        foreach (var row in rows.Where(row => row.Values.Count != columns.Count))
            errors.Add(CatalogImportErrors.Row(
                row.Line,
                null,
                CatalogImportErrors.RowColumns,
                $"Expected {columns.Count} values, found {row.Values.Count}."));

        return errors.Count > 0
            ? Result<CatalogCsvDocument>.Failure(Error.Validation(errors))
            : Result<CatalogCsvDocument>.Success(new CatalogCsvDocument(columns, rows));
    }

    private static Result<CatalogCsvDocument> FileFailure(string code, string message) =>
        Result<CatalogCsvDocument>.Failure(Error.Validation([new ValidationError(FileField, code, message)]));

    private static async Task<byte[]?> ReadBoundedAsync(Stream content, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int read;
        while ((read = await content.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > MaxBytes)
                return null;
            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    private sealed record ParseOutcome(
        List<CatalogCsvRow> Records, int? UnterminatedQuoteLine, bool TooManyRows = false);

    /// <summary>
    /// Separa o texto em registos. O separador é o primeiro <c>;</c> ou <c>,</c> fora de
    /// aspas no cabeçalho, com <c>;</c> preferido quando existem os dois.
    /// </summary>
    private static ParseOutcome Parse(string text)
    {
        var separator = DetectSeparator(text);
        var records = new List<CatalogCsvRow>();
        var values = new List<string>();
        var field = new StringBuilder();
        var line = 1;
        var recordLine = 1;
        var quoteStartLine = 0;
        var inQuotes = false;

        for (var index = 0; index < text.Length; index++)
        {
            var current = text[index];

            if (inQuotes)
            {
                if (current == '"')
                {
                    if (index + 1 < text.Length && text[index + 1] == '"')
                    {
                        field.Append('"');
                        index++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    if (current == '\n')
                        line++;
                    field.Append(current);
                }

                continue;
            }

            if (current == '"')
            {
                inQuotes = true;
                quoteStartLine = line;
            }
            else if (current == separator)
            {
                values.Add(field.ToString());
                field.Clear();
            }
            else if (current is '\r' or '\n')
            {
                if (current == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
                    index++;
                values.Add(field.ToString());
                field.Clear();
                if (!AddRecord(records, recordLine, values))
                    return new ParseOutcome(records, null, TooManyRows: true);
                values = [];
                line++;
                recordLine = line;
            }
            else
            {
                field.Append(current);
            }
        }

        if (inQuotes)
            return new ParseOutcome(records, quoteStartLine);

        if (field.Length > 0 || values.Count > 0)
        {
            values.Add(field.ToString());
            if (!AddRecord(records, recordLine, values))
                return new ParseOutcome(records, null, TooManyRows: true);
        }

        return new ParseOutcome(records, null);
    }

    /// <summary>
    /// Guarda o registo, exceto linhas de dados vazias. Devolve <c>false</c> quando as linhas
    /// de dados passam de <see cref="MaxRows"/>, para parar logo em vez de ler o resto.
    /// </summary>
    private static bool AddRecord(List<CatalogCsvRow> records, int line, List<string> values)
    {
        if (records.Count > 0 && values.All(string.IsNullOrWhiteSpace))
            return true;

        records.Add(new CatalogCsvRow(line, values));
        return records.Count <= MaxRows + 1;
    }

    private static char DetectSeparator(string text)
    {
        var inQuotes = false;
        var sawComma = false;
        foreach (var current in text)
        {
            if (current == '"')
                inQuotes = !inQuotes;
            else if (!inQuotes && current is '\r' or '\n')
                break;
            else if (!inQuotes && current == ';')
                return ';';
            else if (!inQuotes && current == ',')
                sawComma = true;
        }

        return sawComma ? ',' : ';';
    }
}
