using System.Text;
using Application.Features.Administration.CatalogImport;

namespace Application.UnitTests.Features.Administration;

public sealed class CatalogCsvReaderTests
{
    private static readonly string[] Required = ["nome", "proteina"];
    private static readonly string[] Optional = ["descricao"];

    [Fact]
    public async Task Read_ExcelPortugueseFormat_UsesSemicolonAndKeepsLineNumbers()
    {
        var result = await ReadAsync("nome;proteina\nFrango;23,1\n\nArroz;2,7\n");

        Assert.True(result.IsSuccess);
        var document = result.Value;
        Assert.Equal(["nome", "proteina"], document.Columns);
        Assert.Equal([2, 4], document.Rows.Select(row => row.Line));
        Assert.Equal("23,1", document.Get(document.Rows[0], "proteina"));
    }

    [Fact]
    public async Task Read_CommaSeparated_DetectsSeparatorFromHeader()
    {
        var result = await ReadAsync("nome,proteina\r\nFrango,23.1\r\n");

        Assert.True(result.IsSuccess);
        Assert.Equal("Frango", result.Value.Get(result.Value.Rows[0], "nome"));
        Assert.Equal("23.1", result.Value.Get(result.Value.Rows[0], "proteina"));
    }

    [Fact]
    public async Task Read_QuotedValues_KeepSeparatorsQuotesAndLineBreaks()
    {
        var result = await ReadAsync(
            "nome;proteina;descricao\n\"Atum; em lata\";25;\"Diz \"\"natural\"\"\nem água\"\nOvo;12;\n");

        Assert.True(result.IsSuccess);
        var rows = result.Value.Rows;
        Assert.Equal("Atum; em lata", result.Value.Get(rows[0], "nome"));
        Assert.Equal("Diz \"natural\"\nem água", result.Value.Get(rows[0], "descricao"));
        Assert.Equal(4, rows[1].Line);
    }

    [Fact]
    public async Task Read_BomAndAccentedHeader_AreNormalized()
    {
        var bytes = Encoding.UTF8.GetPreamble()
            .Concat(Encoding.UTF8.GetBytes(" Nome ;Proteína;Descrição\nPão;9;\n"))
            .ToArray();

        var result = await CatalogCsvReader.ReadAsync(
            new MemoryStream(bytes), Required, Optional, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(["nome", "proteina", "descricao"], result.Value.Columns);
        Assert.Equal("Pão", result.Value.Get(result.Value.Rows[0], "nome"));
    }

    [Fact]
    public async Task Read_InvalidUtf8_ReturnsEncodingError()
    {
        var bytes = Encoding.Latin1.GetBytes("nome;proteina\nPão;9\n");

        var result = await CatalogCsvReader.ReadAsync(
            new MemoryStream(bytes), Required, Optional, TestContext.Current.CancellationToken);

        AssertSingleError(result, "File", CatalogImportErrors.EncodingInvalid);
    }

    [Fact]
    public async Task Read_MissingUnknownAndRepeatedColumns_ReportsEveryProblem()
    {
        var result = await ReadAsync("nome;nome;calorias\nFrango;Frango;100\n");

        Assert.False(result.IsSuccess);
        var codes = result.Error!.ValidationErrors.Select(error => error.Code).ToList();
        Assert.Contains(CatalogImportErrors.ColumnDuplicate, codes);
        Assert.Contains(CatalogImportErrors.ColumnMissing, codes);
        Assert.Contains(CatalogImportErrors.ColumnUnknown, codes);
        Assert.All(result.Error.ValidationErrors, error => Assert.Equal("File", error.Field));
    }

    [Theory]
    [InlineData("")]
    [InlineData("nome;proteina\n")]
    [InlineData("nome;proteina\n;\n  \n")]
    public async Task Read_WithoutDataRows_ReturnsFileEmpty(string csv)
    {
        var result = await ReadAsync(csv);

        AssertSingleError(result, "File", CatalogImportErrors.FileEmpty);
    }

    [Fact]
    public async Task Read_RowWithWrongNumberOfValues_PointsToTheLine()
    {
        var result = await ReadAsync("nome;proteina\nFrango;23\nArroz\n");

        AssertSingleError(result, "Rows[3]", CatalogImportErrors.RowColumns);
    }

    [Fact]
    public async Task Read_UnterminatedQuote_PointsToWhereItStarts()
    {
        var result = await ReadAsync("nome;proteina\nFrango;23\n\"Arroz;2\n");

        AssertSingleError(result, "Rows[3]", CatalogImportErrors.QuoteUnterminated);
    }

    [Fact]
    public async Task Read_MoreThanMaxRows_IsRejected()
    {
        var csv = new StringBuilder("nome;proteina\n");
        for (var index = 0; index <= CatalogCsvReader.MaxRows; index++)
            csv.Append("Item ").Append(index).Append(";1\n");

        var result = await ReadAsync(csv.ToString());

        AssertSingleError(result, "File", CatalogImportErrors.TooManyRows);
    }

    [Fact]
    public async Task Read_HeaderWithThousandsOfEmptyColumns_ReturnsOneError()
    {
        var result = await ReadAsync("nome;proteina" + new string(';', 5000) + "\nOvo;1\n");

        AssertSingleError(result, "File", CatalogImportErrors.ColumnUnknown);
    }

    [Fact]
    public async Task Read_HeaderAboveColumnLimit_ReturnsOneErrorInsteadOfOnePerColumn()
    {
        var extra = string.Join(';', Enumerable.Range(1, CatalogCsvReader.MaxColumns).Select(index => $"c{index}"));

        var result = await ReadAsync($"nome;proteina;{extra}\nOvo;1\n");

        AssertSingleError(result, "File", CatalogImportErrors.ColumnUnknown);
    }

    [Fact]
    public async Task Read_RepeatedUnknownColumn_IsReportedOnce()
    {
        var result = await ReadAsync("nome;proteina;;\nOvo;1;;\n");

        AssertSingleError(result, "File", CatalogImportErrors.ColumnUnknown);
    }

    [Fact]
    public async Task Read_ManyBlankLines_AreIgnoredWithoutCountingAsRows()
    {
        var result = await ReadAsync("nome;proteina\n" + new string('\n', 200_000) + "Ovo;1\n");

        Assert.True(result.IsSuccess);
        Assert.Equal(200_002, Assert.Single(result.Value.Rows).Line);
    }

    [Fact]
    public async Task Read_FileLargerThanLimit_IsRejected()
    {
        var bytes = new byte[CatalogCsvReader.MaxBytes + 1];
        Array.Fill(bytes, (byte)'a');

        var result = await CatalogCsvReader.ReadAsync(
            new MemoryStream(bytes), Required, Optional, TestContext.Current.CancellationToken);

        AssertSingleError(result, "File", CatalogImportErrors.FileTooLarge);
    }

    [Theory]
    [InlineData("25,5", 25.5)]
    [InlineData("25.5", 25.5)]
    [InlineData(" 3 ", 3)]
    public void TryParseDecimal_AcceptsCommaOrPoint(string raw, double expected)
    {
        Assert.True(CatalogImportValues.TryParseDecimal(raw, out var value));
        Assert.Equal((decimal)expected, value);
    }

    [Theory]
    [InlineData("1.000,5")]
    [InlineData("abc")]
    [InlineData("1e3")]
    public void TryParseDecimal_RejectsAmbiguousOrInvalidNumbers(string raw)
    {
        Assert.False(CatalogImportValues.TryParseDecimal(raw, out _));
    }

    [Theory]
    [InlineData("Feijão  Preto", "feijao preto")]
    [InlineData("  PÃO de Forma ", "pao de forma")]
    [InlineData("Açúcar", "acucar")]
    public void NameKey_IgnoresCaseAccentsAndExtraSpaces(string name, string expected)
    {
        Assert.Equal(expected, CatalogImportName.Key(name));
    }

    [Theory]
    [InlineData("=HYPERLINK(\"x\")", true)]
    [InlineData(" +1", true)]
    [InlineData("@SUM", true)]
    [InlineData("-5", true)]
    [InlineData("Ovo", false)]
    [InlineData("", false)]
    public void LooksLikeFormula_DetectsSpreadsheetPrefixes(string name, bool expected)
    {
        Assert.Equal(expected, CatalogImportValues.LooksLikeFormula(name));
    }

    private static Task<Application.Results.Result<CatalogCsvDocument>> ReadAsync(string csv) =>
        CatalogCsvReader.ReadAsync(
            new MemoryStream(Encoding.UTF8.GetBytes(csv)),
            Required,
            Optional,
            TestContext.Current.CancellationToken);

    private static void AssertSingleError(
        Application.Results.Result<CatalogCsvDocument> result, string field, string code)
    {
        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Error!.ValidationErrors);
        Assert.Equal(field, error.Field);
        Assert.Equal(code, error.Code);
    }
}
