namespace Application.Features.Administration.CatalogImport;

/// <summary>Motivos para uma linha válida não ser criada.</summary>
public static class CatalogImportSkipReasons
{
    /// <summary>Já existe um item global com o mesmo nome, ativo ou arquivado.</summary>
    public const string AlreadyExists = "already_exists";

    /// <summary>O mesmo nome aparece numa linha anterior do ficheiro.</summary>
    public const string DuplicateInFile = "duplicate_in_file";
}

/// <summary>Linha válida que não foi criada, com o motivo.</summary>
public sealed record CatalogImportSkippedRowDto(int Line, string Name, string Reason);

/// <summary>
/// Resultado de uma importação. Em <see cref="DryRun"/>, <see cref="Created"/> é o número
/// de itens que seriam criados e nada foi gravado.
/// </summary>
public sealed record CatalogImportReportDto(
    bool DryRun,
    int TotalRows,
    int Created,
    IReadOnlyList<CatalogImportSkippedRowDto> Skipped);

/// <summary>Resultado da escrita: quantos itens foram (ou seriam) criados e quais foram saltados.</summary>
public sealed record CatalogImportStoreOutcome(
    int Created,
    IReadOnlyList<CatalogImportSkippedRowDto> Skipped);
