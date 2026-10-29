using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Errors;
using Application.Features.Administration.CatalogImport;
using Application.Features.Nutrition.Foods.Abstractions;
using Application.Features.Nutrition.Foods.CreateGlobalFood;
using Application.Results;
using FluentValidation;

namespace Application.Features.Nutrition.Foods.ImportGlobalFoods;

/// <summary>
/// Importa alimentos globais a partir de um CSV. É tudo ou nada: se uma linha tiver erro,
/// nenhuma é gravada e a resposta lista todos os erros por linha.
/// </summary>
/// <remarks>
/// Cada linha passa pelo mesmo validador da criação manual, por isso um alimento importado
/// cumpre exatamente as regras de um criado no editor.
/// </remarks>
public sealed class ImportGlobalFoodsHandler
{
    /// <summary>Colunas obrigatórias do CSV de alimentos.</summary>
    public static readonly IReadOnlyCollection<string> RequiredColumns =
        ["nome", "proteina", "hidratos", "gordura"];

    /// <summary>Colunas opcionais do CSV de alimentos.</summary>
    public static readonly IReadOnlyCollection<string> OptionalColumns =
        ["descricao", "fibra", "porcao_g"];

    private static readonly IReadOnlyDictionary<string, string> ColumnByProperty =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [nameof(CreateGlobalFoodCommand.Name)] = "nome",
            [nameof(CreateGlobalFoodCommand.Description)] = "descricao",
            [nameof(CreateGlobalFoodCommand.Protein)] = "proteina",
            [nameof(CreateGlobalFoodCommand.Carbs)] = "hidratos",
            [nameof(CreateGlobalFoodCommand.Fats)] = "gordura",
            [nameof(CreateGlobalFoodCommand.Fiber)] = "fibra",
            [nameof(CreateGlobalFoodCommand.DefaultServingGrams)] = "porcao_g",
            ["Macros"] = "macros",
        };

    private readonly IValidator<CreateGlobalFoodCommand> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly IGlobalFoodStore _store;

    public ImportGlobalFoodsHandler(
        IValidator<CreateGlobalFoodCommand> validator,
        ITenantContext tenantContext,
        IClock clock,
        IGlobalFoodStore store)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<Result<CatalogImportReportDto>> HandleAsync(
        ImportGlobalFoodsCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var actor = ActorAuthorization.RequireAdministrator(
            _tenantContext, NutritionErrors.AdministratorOnly);
        if (!actor.IsSuccess)
            return Result<CatalogImportReportDto>.Failure(actor.Error!);

        var read = await CatalogCsvReader.ReadAsync(
            command.Content, RequiredColumns, OptionalColumns, cancellationToken);
        if (!read.IsSuccess)
            return Result<CatalogImportReportDto>.Failure(read.Error!);

        var document = read.Value;
        var errors = new List<ValidationError>();
        var items = new List<GlobalFoodImportItem>();

        foreach (var row in document.Rows)
        {
            var item = await ReadRowAsync(document, row, errors, cancellationToken);
            if (item is not null)
                items.Add(item);
        }

        if (errors.Count > 0)
            return Result<CatalogImportReportDto>.Failure(Error.Validation(errors));

        var (unique, skippedInFile) = CatalogImportValues.SplitInFileDuplicates(
            items, item => item.Line, item => item.Name);

        var outcome = await _store.ImportAsync(
            actor.Value.UserId, unique, command.DryRun, _clock.UtcNow, cancellationToken);

        return Result<CatalogImportReportDto>.Success(new CatalogImportReportDto(
            command.DryRun,
            document.Rows.Count,
            outcome.Created,
            [.. skippedInFile.Concat(outcome.Skipped).OrderBy(skipped => skipped.Line)]));
    }

    /// <summary>Converte e valida uma linha; devolve <c>null</c> e acumula os erros quando falha.</summary>
    private async Task<GlobalFoodImportItem?> ReadRowAsync(
        CatalogCsvDocument document,
        CatalogCsvRow row,
        List<ValidationError> errors,
        CancellationToken cancellationToken)
    {
        var errorsBefore = errors.Count;
        var name = document.Get(row, "nome");

        var protein = RequiredNumber(document, row, "proteina", errors);
        var carbs = RequiredNumber(document, row, "hidratos", errors);
        var fats = RequiredNumber(document, row, "gordura", errors);
        var fiber = OptionalNumber(document, row, "fibra", errors);
        var serving = OptionalNumber(document, row, "porcao_g", errors);

        if (CatalogImportValues.LooksLikeFormula(name))
            errors.Add(CatalogImportErrors.Row(
                row.Line, "nome", CatalogImportErrors.NameFormula, "The name cannot start with = + - or @."));

        if (errors.Count > errorsBefore)
            return null;

        var create = new CreateGlobalFoodCommand(
            name,
            CatalogImportValues.Optional(document.Get(row, "descricao")),
            protein!.Value,
            carbs!.Value,
            fats!.Value,
            fiber,
            serving);

        var validation = await _validator.ValidateAsync(create, cancellationToken);
        if (!validation.IsValid)
        {
            errors.AddRange(CatalogImportValues.ToRowErrors(row.Line, validation, ColumnByProperty));
            return null;
        }

        return new GlobalFoodImportItem(
            row.Line,
            create.Name.Trim(),
            create.Description,
            create.Protein,
            create.Carbs,
            create.Fats,
            create.Fiber,
            create.DefaultServingGrams);
    }

    private static decimal? RequiredNumber(
        CatalogCsvDocument document, CatalogCsvRow row, string column, List<ValidationError> errors)
    {
        var raw = document.Get(row, column);
        if (string.IsNullOrWhiteSpace(raw))
        {
            errors.Add(CatalogImportErrors.Row(
                row.Line, column, CatalogImportErrors.NumberRequired, "A number is required."));
            return null;
        }

        return OptionalNumber(document, row, column, errors);
    }

    private static decimal? OptionalNumber(
        CatalogCsvDocument document, CatalogCsvRow row, string column, List<ValidationError> errors)
    {
        if (CatalogImportValues.TryParseDecimal(document.Get(row, column), out var value))
            return value;

        errors.Add(CatalogImportErrors.Row(
            row.Line, column, CatalogImportErrors.NumberInvalid, "The value is not a valid number."));
        return null;
    }
}
