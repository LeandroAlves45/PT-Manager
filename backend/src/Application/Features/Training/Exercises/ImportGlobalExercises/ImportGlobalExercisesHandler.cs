using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Errors;
using Application.Features.Administration.CatalogImport;
using Application.Features.Training.Exercises.Abstractions;
using Application.Features.Training.Exercises.CreateGlobalExercise;
using Application.Results;
using Domain.ValueObjects;
using FluentValidation;

namespace Application.Features.Training.Exercises.ImportGlobalExercises;

/// <summary>
/// Importa exercícios globais a partir de um CSV. É tudo ou nada: se uma linha tiver erro,
/// nenhuma é gravada e a resposta lista todos os erros por linha.
/// </summary>
/// <remarks>
/// Cada linha passa pelo validador da criação manual. A importação não aceita vídeo: o
/// vídeo de um exercício global é enviado depois, no editor do catálogo.
/// </remarks>
public sealed class ImportGlobalExercisesHandler
{
    /// <summary>Colunas obrigatórias do CSV de exercícios.</summary>
    public static readonly IReadOnlyCollection<string> RequiredColumns = ["nome"];

    /// <summary>Colunas opcionais do CSV de exercícios.</summary>
    public static readonly IReadOnlyCollection<string> OptionalColumns =
        ["descricao", "grupos_musculares", "equipamento", "dificuldade"];

    /// <summary>
    /// Únicos valores aceites na coluna <c>dificuldade</c>. A comparação ignora acentos e
    /// maiúsculas, e o valor gravado é sempre esta forma.
    /// </summary>
    public static readonly IReadOnlyList<string> DifficultyLevels = ["Iniciante", "Intermédio", "Avançado"];

    private static readonly IReadOnlyDictionary<string, string> ColumnByProperty =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [nameof(CreateGlobalExerciseCommand.Name)] = "nome",
            [nameof(CreateGlobalExerciseCommand.Description)] = "descricao",
            [nameof(CreateGlobalExerciseCommand.MuscleGroups)] = "grupos_musculares",
            [nameof(CreateGlobalExerciseCommand.Equipment)] = "equipamento",
            [nameof(CreateGlobalExerciseCommand.DifficultyLevel)] = "dificuldade",
        };

    private readonly IValidator<CreateGlobalExerciseCommand> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly IGlobalExerciseStore _store;

    public ImportGlobalExercisesHandler(
        IValidator<CreateGlobalExerciseCommand> validator,
        ITenantContext tenantContext,
        IClock clock,
        IGlobalExerciseStore store)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<Result<CatalogImportReportDto>> HandleAsync(
        ImportGlobalExercisesCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var actor = ActorAuthorization.RequireAdministrator(
            _tenantContext, TrainingErrors.AdministratorOnly);
        if (!actor.IsSuccess)
            return Result<CatalogImportReportDto>.Failure(actor.Error!);

        var read = await CatalogCsvReader.ReadAsync(
            command.Content, RequiredColumns, OptionalColumns, cancellationToken);
        if (!read.IsSuccess)
            return Result<CatalogImportReportDto>.Failure(read.Error!);

        var document = read.Value;
        var errors = new List<ValidationError>();
        var items = new List<GlobalExerciseImportItem>();

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
    private async Task<GlobalExerciseImportItem?> ReadRowAsync(
        CatalogCsvDocument document,
        CatalogCsvRow row,
        List<ValidationError> errors,
        CancellationToken cancellationToken)
    {
        var errorsBefore = errors.Count;
        var name = document.Get(row, "nome");
        var difficultyRaw = CatalogImportValues.Optional(document.Get(row, "dificuldade"));
        string? difficulty = null;

        if (difficultyRaw is not null)
        {
            difficulty = DifficultyLevels.FirstOrDefault(level =>
                CatalogImportName.Key(level) == CatalogImportName.Key(difficultyRaw));
            if (difficulty is null)
                errors.Add(CatalogImportErrors.Row(
                    row.Line,
                    "dificuldade",
                    CatalogImportErrors.DifficultyInvalid,
                    "Use Iniciante, Intermédio or Avançado."));
        }

        if (CatalogImportValues.LooksLikeFormula(name))
            errors.Add(CatalogImportErrors.Row(
                row.Line, "nome", CatalogImportErrors.NameFormula, "The name cannot start with = + - or @."));

        var create = new CreateGlobalExerciseCommand(
            name,
            CatalogImportValues.Optional(document.Get(row, "descricao")),
            CatalogImportValues.Optional(document.Get(row, "grupos_musculares")),
            CatalogImportValues.Optional(document.Get(row, "equipamento")),
            difficulty,
            VideoUrl: null);

        var validation = await _validator.ValidateAsync(create, cancellationToken);
        if (!validation.IsValid)
            errors.AddRange(CatalogImportValues.ToRowErrors(row.Line, validation, ColumnByProperty));

        if (errors.Count > errorsBefore)
            return null;

        return new GlobalExerciseImportItem(
            row.Line,
            create.Name.Trim(),
            create.Description,
            MuscleGroupCatalog.Normalize(create.MuscleGroups),
            create.Equipment,
            create.DifficultyLevel);
    }
}
