using Application.Features.Administration.CatalogImport;

namespace Api.Contracts.Administration;

/// <summary>Linha válida do CSV que não foi criada.</summary>
/// <param name="Line">Número da linha no ficheiro.</param>
/// <param name="Name">Nome lido nessa linha.</param>
/// <param name="Reason"><c>already_exists</c> ou <c>duplicate_in_file</c>.</param>
public sealed record CatalogImportSkippedRowResponse(int Line, string Name, string Reason);

/// <summary>
/// Resultado da importação CSV do catálogo global. Com <c>dry_run</c>, <c>created</c> é o
/// número de itens que seriam criados e nada foi gravado.
/// </summary>
public sealed record CatalogImportReportResponse(
    bool DryRun,
    int TotalRows,
    int Created,
    IReadOnlyList<CatalogImportSkippedRowResponse> Skipped)
{
    /// <summary>Projeta o DTO da Application no contrato da API.</summary>
    public static CatalogImportReportResponse From(CatalogImportReportDto report)
    {
        ArgumentNullException.ThrowIfNull(report);

        return new(
            report.DryRun,
            report.TotalRows,
            report.Created,
            [.. report.Skipped.Select(row => new CatalogImportSkippedRowResponse(row.Line, row.Name, row.Reason))]);
    }
}

/// <summary>Limites HTTP do upload do CSV de importação.</summary>
internal static class CatalogImportUpload
{
    /// <summary>1 MiB do ficheiro mais folga para as fronteiras e cabeçalhos multipart.</summary>
    internal const long MaxRequestBytes = CatalogCsvReader.MaxBytes + 64 * 1024;
    internal const int MaxPartHeadersBytes = 8 * 1024;
    internal const int MaxFormValues = 4;
}
