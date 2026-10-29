using System.Text;
using Application.Common.Abstractions;
using Application.Features.Administration.CatalogImport;
using Application.Features.Nutrition;
using Application.Features.Nutrition.Foods.Abstractions;
using Application.Features.Nutrition.Foods.CreateGlobalFood;
using Application.Features.Nutrition.Foods.ImportGlobalFoods;
using Application.Features.Training.Exercises.Abstractions;
using Application.Features.Training.Exercises.CreateGlobalExercise;
using Application.Features.Training.Exercises.ImportGlobalExercises;

namespace Application.UnitTests.Features.Administration;

public sealed class CatalogImportHandlerTests
{
    private const string FoodHeader = "nome;descricao;proteina;hidratos;gordura;fibra;porcao_g\n";
    private const string ExerciseHeader = "nome;descricao;grupos_musculares;equipamento;dificuldade\n";

    [Fact]
    public async Task ImportFoods_ValidFile_ParsesDecimalCommaAndPassesItemsToStore()
    {
        var store = new RecordingFoodStore();

        var result = await ImportFoodsAsync(
            store, FoodHeader + "Frango, peito;Cru;23,1;0;1,2;;100\nArroz;;2.7;28;0.3;0,4;\n");

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.TotalRows);
        Assert.Equal(2, result.Value.Created);
        var frango = store.Items[0];
        Assert.Equal((2, "Frango, peito", 23.1m, 1.2m, 100m), (frango.Line, frango.Name, frango.Protein, frango.Fats, frango.DefaultServingGrams));
        Assert.Null(frango.Fiber);
        Assert.Equal(0.4m, store.Items[1].Fiber);
        Assert.Null(store.Items[1].Description);
    }

    [Fact]
    public async Task ImportFoods_AnyInvalidRow_ReportsAllErrorsAndNeverCallsStore()
    {
        var store = new RecordingFoodStore();

        var result = await ImportFoodsAsync(
            store,
            FoodHeader +
            "Frango;;23;0;1;;\n" +
            ";;120;0;0;;\n" +
            "Arroz;;abc;;0;;\n" +
            "=HYPERLINK(1);;1;1;1;;\n" +
            "Óleo;;0;0;100;;2000\n");

        Assert.False(result.IsSuccess);
        Assert.False(store.WasCalled);
        var fields = result.Error!.ValidationErrors.Select(error => (error.Field, error.Code)).ToList();
        Assert.Contains(("Rows[3].nome", "food_name_required"), fields);
        Assert.Contains(("Rows[3].proteina", "food_protein_out_of_range"), fields);
        Assert.Contains(("Rows[3].macros", "food_macros_exceed_total"), fields);
        Assert.Contains(("Rows[4].proteina", CatalogImportErrors.NumberInvalid), fields);
        Assert.Contains(("Rows[4].hidratos", CatalogImportErrors.NumberRequired), fields);
        Assert.Contains(("Rows[5].nome", CatalogImportErrors.NameFormula), fields);
        Assert.Contains(("Rows[6].porcao_g", "food_default_serving_out_of_range"), fields);
        Assert.DoesNotContain(fields, field => field.Field.StartsWith("Rows[2]", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ImportFoods_DuplicateNamesInFile_KeepsFirstAndSkipsTheRest()
    {
        var store = new RecordingFoodStore();

        var result = await ImportFoodsAsync(
            store, FoodHeader + "Feijão preto;;9;14;0,5;;\nFEIJAO  PRETO;;9;14;0,5;;\nOvo;;12;1;10;;\n");

        Assert.True(result.IsSuccess);
        Assert.Equal(["Feijão preto", "Ovo"], store.Items.Select(item => item.Name));
        var skipped = Assert.Single(result.Value.Skipped);
        Assert.Equal((3, CatalogImportSkipReasons.DuplicateInFile), (skipped.Line, skipped.Reason));
        Assert.Equal(2, result.Value.Created);
    }

    [Fact]
    public async Task ImportFoods_DryRun_IsForwardedAndReportedAsSuch()
    {
        var store = new RecordingFoodStore();

        var result = await ImportFoodsAsync(store, FoodHeader + "Ovo;;12;1;10;;\n", dryRun: true);

        Assert.True(result.IsSuccess);
        Assert.True(store.DryRun);
        Assert.True(result.Value.DryRun);
    }

    [Fact]
    public async Task ImportFoods_StoreSkips_AreMergedInLineOrder()
    {
        var store = new RecordingFoodStore { ExistingNames = ["ovo"] };

        var result = await ImportFoodsAsync(
            store, FoodHeader + "Ovo;;12;1;10;;\nArroz;;2;28;0;;\nArroz;;2;28;0;;\n");

        Assert.True(result.IsSuccess);
        Assert.Equal([2, 4], result.Value.Skipped.Select(row => row.Line));
        Assert.Equal(1, result.Value.Created);
    }

    [Theory]
    [InlineData("trainer", true)]
    [InlineData("superuser", false)]
    public async Task ImportFoods_NonAdministrator_IsForbiddenBeforeReadingTheFile(string role, bool administrative)
    {
        var store = new RecordingFoodStore();
        var handler = new ImportGlobalFoodsHandler(
            new CreateGlobalFoodCommandValidator(),
            new TestTenantContext(role, administrative),
            new FixedClock(),
            store);

        var result = await handler.HandleAsync(
            new ImportGlobalFoodsCommand(new ThrowingStream(), false), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(NutritionErrors.AdministratorOnly.Code, result.Error!.Code);
        Assert.False(store.WasCalled);
    }

    [Fact]
    public async Task ImportExercises_ValidFile_NormalizesDifficultyAndMuscleGroups()
    {
        var store = new RecordingExerciseStore();

        var result = await ImportExercisesAsync(
            store,
            ExerciseHeader +
            "Supino plano;Deitado no banco;triceps, chest;Barra;intermedio\n" +
            "Prancha;;core;;\n");

        Assert.True(result.IsSuccess);
        var supino = store.Items[0];
        Assert.Equal("Intermédio", supino.DifficultyLevel);
        Assert.Equal("chest,triceps", supino.MuscleGroups);
        Assert.Null(store.Items[1].DifficultyLevel);
        Assert.Null(store.Items[1].Equipment);
    }

    [Fact]
    public async Task ImportExercises_InvalidDifficultyOrMuscleGroup_IsRejected()
    {
        var store = new RecordingExerciseStore();

        var result = await ImportExercisesAsync(
            store, ExerciseHeader + "Supino;;peito;Barra;intermediate\n");

        Assert.False(result.IsSuccess);
        Assert.False(store.WasCalled);
        var fields = result.Error!.ValidationErrors.Select(error => (error.Field, error.Code)).ToList();
        Assert.Contains(("Rows[2].dificuldade", CatalogImportErrors.DifficultyInvalid), fields);
        Assert.Contains(("Rows[2].grupos_musculares", "exercise_muscle_groups_invalid"), fields);
    }

    [Fact]
    public async Task ImportExercises_VideoColumn_IsNotAccepted()
    {
        var result = await ImportExercisesAsync(
            new RecordingExerciseStore(), "nome;video_url\nSupino;https://example.com/v.mp4\n");

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Error!.ValidationErrors);
        Assert.Equal(CatalogImportErrors.ColumnUnknown, error.Code);
    }

    private static Task<Application.Results.Result<CatalogImportReportDto>> ImportFoodsAsync(
        RecordingFoodStore store, string csv, bool dryRun = false) =>
        new ImportGlobalFoodsHandler(
                new CreateGlobalFoodCommandValidator(),
                new TestTenantContext("superuser", true),
                new FixedClock(),
                store)
            .HandleAsync(
                new ImportGlobalFoodsCommand(new MemoryStream(Encoding.UTF8.GetBytes(csv)), dryRun),
                TestContext.Current.CancellationToken);

    private static Task<Application.Results.Result<CatalogImportReportDto>> ImportExercisesAsync(
        RecordingExerciseStore store, string csv) =>
        new ImportGlobalExercisesHandler(
                new CreateGlobalExerciseCommandValidator(),
                new TestTenantContext("superuser", true),
                new FixedClock(),
                store)
            .HandleAsync(
                new ImportGlobalExercisesCommand(new MemoryStream(Encoding.UTF8.GetBytes(csv)), false),
                TestContext.Current.CancellationToken);

    private sealed class TestTenantContext(string role, bool isAdministrative) : ITenantContext
    {
        public Guid? TrainerId { get; } = role == "trainer" ? Guid.NewGuid() : null;
        public Guid? UserId { get; } = Guid.NewGuid();
        public string? Role { get; } = role;
        public TenantOrigin Origin => TenantOrigin.Http;
        public bool IsAdministrative { get; } = isAdministrative;
    }

    private sealed class FixedClock : IClock
    {
        public DateTime UtcNow => new(2026, 10, 29, 10, 0, 0, DateTimeKind.Utc);
    }

    /// <summary>Falha se for lida: prova que a autorização acontece antes do ficheiro.</summary>
    private sealed class ThrowingStream : MemoryStream
    {
        public override int Read(byte[] buffer, int offset, int count) => throw new InvalidOperationException();

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();
    }

    /// <summary>Simula o store: salta os nomes em <see cref="ExistingNames"/> e conta o resto.</summary>
    private abstract class RecordingStore<TItem>
    {
        public List<TItem> Items { get; } = [];
        public string[] ExistingNames { get; init; } = [];
        public bool WasCalled { get; private set; }
        public bool DryRun { get; private set; }

        protected CatalogImportStoreOutcome Record(
            IReadOnlyList<TItem> items, bool dryRun, Func<TItem, (int Line, string Name)> describe)
        {
            WasCalled = true;
            DryRun = dryRun;
            Items.AddRange(items);
            var skipped = items
                .Select(describe)
                .Where(item => ExistingNames.Contains(CatalogImportName.Key(item.Name)))
                .Select(item => new CatalogImportSkippedRowDto(item.Line, item.Name, CatalogImportSkipReasons.AlreadyExists))
                .ToList();
            return new CatalogImportStoreOutcome(items.Count - skipped.Count, skipped);
        }
    }

    private sealed class RecordingFoodStore : RecordingStore<GlobalFoodImportItem>, IGlobalFoodStore
    {
        public Task<CatalogImportStoreOutcome> ImportAsync(
            Guid actorUserId, IReadOnlyList<GlobalFoodImportItem> items, bool dryRun, DateTime now, CancellationToken cancellationToken) =>
            Task.FromResult(Record(items, dryRun, item => (item.Line, item.Name)));

        public Task<GlobalFoodStoreResult> CreateAsync(Guid actorUserId, string name, string? description, decimal protein, decimal carbs, decimal fats, decimal? fiber, decimal? defaultServingGrams, DateTime now, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<GlobalFoodStoreResult> UpdateAsync(Guid actorUserId, Guid foodId, string name, string? description, decimal protein, decimal carbs, decimal fats, decimal? fiber, decimal? defaultServingGrams, DateTime now, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<GlobalFoodStoreResult> SetActiveAsync(Guid actorUserId, Guid foodId, bool isActive, DateTime now, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<GlobalFoodStoreResult> DeleteAsync(Guid actorUserId, Guid foodId, DateTime now, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingExerciseStore : RecordingStore<GlobalExerciseImportItem>, IGlobalExerciseStore
    {
        public Task<CatalogImportStoreOutcome> ImportAsync(
            Guid actorUserId, IReadOnlyList<GlobalExerciseImportItem> items, bool dryRun, DateTime now, CancellationToken cancellationToken) =>
            Task.FromResult(Record(items, dryRun, item => (item.Line, item.Name)));

        public Task<GlobalExerciseStoreResult> CreateAsync(Guid actorUserId, string name, string? description, string? muscleGroups, string? equipment, string? difficultyLevel, string? videoUrl, DateTime now, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<GlobalExerciseStoreResult> UpdateAsync(Guid actorUserId, Guid exerciseId, string name, string? description, string? muscleGroups, string? equipment, string? difficultyLevel, string? videoUrl, DateTime now, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<GlobalExerciseStoreResult> SetActiveAsync(Guid actorUserId, Guid exerciseId, bool isActive, DateTime now, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<GlobalExerciseStoreResult> DeleteAsync(Guid actorUserId, Guid exerciseId, DateTime now, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
