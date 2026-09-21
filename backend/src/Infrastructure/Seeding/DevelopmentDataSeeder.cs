using Application.Common.Abstractions;
using Domain.Entities.Administration;
using Domain.Entities.Assessments;
using Domain.Entities.Billing;
using Domain.Entities.Clients;
using Domain.Entities.Identity;
using Domain.Entities.Nutrition;
using Domain.Entities.Sessions;
using Domain.Entities.Supplements;
using Domain.Entities.Training;
using Domain.Services;
using Domain.ValueObjects;
using Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Seeding;

/// <summary>
/// Cria um ambiente de desenvolvimento completo a partir de uma base de dados vazia.
/// </summary>
/// <remarks>
/// <para>
/// É idempotente depois de validar o conjunto completo. A existência isolada do
/// superuser não prova que o seed terminou e é tratada como estado parcial inválido.
/// </para>
/// </remarks>
public sealed class DevelopmentDataSeeder
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DevelopmentSeedOptions _options;
    private readonly ILogger<DevelopmentDataSeeder> _logger;

    public DevelopmentDataSeeder(
        IServiceScopeFactory scopeFactory,
        IOptions<DevelopmentSeedOptions> options,
        ILogger<DevelopmentDataSeeder> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _options = options.Value;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Semeia o ambiente, se ainda não tiver sido semeado.</summary>
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var superuserId = await EnsureSuperuserAsync(now, cancellationToken);
        if (superuserId is null)
        {
            await ValidateCompleteSeedAsync(cancellationToken);
            _logger.LogInformation("Development seed completed successfully; not created duplicates.");
            return;
        }

        var catalog = await SeedGlobalCatalogAsync(superuserId.Value, now, cancellationToken);
        await SeedTrainerTenantAsync(catalog, now, cancellationToken);

        _logger.LogInformation(
            "Development seed completed successfully. Accounts: {Superuser}, {Trainer}, {Client}",
            _options.SuperuserEmail,
            _options.TrainerEmail,
            _options.ClientEmail);
    }

    /// <summary>
    /// Valida os elementos mínimos que demonstram que todas as fases do seed terminaram.
    /// A validação aceita dados adicionais criados pelo programador, mas nunca confunde a
    /// existência isolada do superuser com um seed completo.
    /// </summary>
    private async Task ValidateCompleteSeedAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        Establish(
            scope,
            trainerId: null,
            userId: null,
            role: "superuser",
            isAdministrative: true);

        var context = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();
        var expectedEmails = new[]
        {
            new EmailAddress(_options.SuperuserEmail).Normalized,
            new EmailAddress(_options.TrainerEmail).Normalized,
            new EmailAddress(_options.ClientEmail).Normalized,
        };

        var users = await context.Users
            .IgnoreQueryFilters()
            .Where(user => expectedEmails.Contains(user.NormalizedEmail))
            .Select(user => new
            {
                user.Id,
                user.NormalizedEmail,
                user.Role,
                user.EmailConfirmed
            })
            .ToListAsync(cancellationToken);

        var missing = new List<string>();
        RequireUser(expectedEmails[0], "superuser");
        RequireUser(expectedEmails[1], "trainer");
        RequireUser(expectedEmails[2], "client");

        var trainerId = users
            .Where(user => user.NormalizedEmail == expectedEmails[1])
            .Select(user => (Guid?)user.Id)
            .SingleOrDefault();
        var clientUserId = users
            .Where(user => user.NormalizedEmail == expectedEmails[2])
            .Select(user => (Guid?)user.Id)
            .SingleOrDefault();
        var clientId = clientUserId.HasValue
            ? await context.Clients
                .IgnoreQueryFilters()
                .Where(client => client.UserId == clientUserId.Value)
                .Select(client => (Guid?)client.Id)
                .SingleOrDefaultAsync(cancellationToken)
            : null;
        if (trainerId.HasValue)
        {
            await RequireCountAsync(
                "clients",
                context.Clients.IgnoreQueryFilters()
                    .Where(entity =>
                        entity.OwnerTrainerId == trainerId.Value
                        && (entity.Name == "João Pereira" || entity.Name == "Ana Costa")),
                minimum: 2);
            await RequireCountAsync(
                "trainer subscription",
                context.TrainerSubscriptions.IgnoreQueryFilters()
                    .Where(entity => entity.TrainerId == trainerId.Value),
                minimum: 1);
            await RequireCountAsync(
                "trainer settings",
                context.TrainerSettings.IgnoreQueryFilters()
                    .Where(entity => entity.TrainerId == trainerId.Value),
                minimum: 1);
            await RequireCountAsync(
                "initial assessment",
                context.InitialAssessments.IgnoreQueryFilters()
                    .Where(entity => entity.OwnerTrainerId == trainerId.Value),
                minimum: 1);
            await RequireCountAsync(
                "private exercise",
                context.Exercises.IgnoreQueryFilters()
                    .Where(entity =>
                        entity.OwnerTrainerId == trainerId.Value
                        && entity.Name == "Remada curvada"),
                minimum: 1);
            await RequireCountAsync(
                "private food",
                context.Foods.IgnoreQueryFilters()
                    .Where(entity =>
                        entity.OwnerTrainerId == trainerId.Value
                        && entity.Name == "Batido pós-treino"),
                minimum: 1);
            await RequireCountAsync(
                "private supplement",
                context.Supplements.IgnoreQueryFilters()
                    .Where(entity =>
                        entity.OwnerTrainerId == trainerId.Value
                        && entity.Name == "Multivitamínico"),
                minimum: 1);
            await RequireCountAsync(
                "training plan",
                context.TrainingPlans.IgnoreQueryFilters()
                    .Where(entity =>
                        entity.OwnerTrainerId == trainerId.Value
                        && entity.Name == "Hipertrofia - bloco 1"),
                minimum: 1);

            var trainingPlanIds = context.TrainingPlans
                .IgnoreQueryFilters()
                .Where(entity =>
                    entity.OwnerTrainerId == trainerId.Value
                    && entity.Name == "Hipertrofia - bloco 1")
                .Select(entity => entity.Id);
            var trainingDayIds = context.TrainingPlanDays
                .IgnoreQueryFilters()
                .Where(entity => trainingPlanIds.Contains(entity.TrainingPlanId))
                .Select(entity => entity.Id);
            var trainingExerciseIds = context.TrainingPlanDayExercises
                .IgnoreQueryFilters()
                .Where(entity => trainingDayIds.Contains(entity.TrainingPlanDayId))
                .Select(entity => entity.Id);

            await RequireCountAsync(
                "training plan days",
                context.TrainingPlanDays.IgnoreQueryFilters()
                    .Where(entity => trainingPlanIds.Contains(entity.TrainingPlanId)),
                minimum: 2);
            await RequireCountAsync(
                "planned exercise sets",
                context.ExerciseSets.IgnoreQueryFilters()
                    .Where(entity => trainingExerciseIds.Contains(entity.TrainingPlanDayExerciseId)),
                minimum: 10);
            await RequireCountAsync(
                "meal plan",
                context.MealPlans.IgnoreQueryFilters()
                    .Where(entity =>
                        entity.OwnerTrainerId == trainerId.Value
                        && entity.Name == "Manutenção - 2400 kcal"),
                minimum: 1);

            var mealPlanIds = context.MealPlans
                .IgnoreQueryFilters()
                .Where(entity =>
                    entity.OwnerTrainerId == trainerId.Value
                    && entity.Name == "Manutenção - 2400 kcal")
                .Select(entity => entity.Id);
            var mealIds = context.MealPlanMeals
                .IgnoreQueryFilters()
                .Where(entity => mealPlanIds.Contains(entity.MealPlanId))
                .Select(entity => entity.Id);
            await RequireCountAsync(
                "meal plan meals",
                context.MealPlanMeals.IgnoreQueryFilters()
                    .Where(entity => mealPlanIds.Contains(entity.MealPlanId)),
                minimum: 1);
            await RequireCountAsync(
                "meal plan items",
                context.MealPlanMealItems.IgnoreQueryFilters()
                    .Where(entity => mealIds.Contains(entity.MealPlanMealId)),
                minimum: 2);
            await RequireCountAsync(
                "sessions",
                context.Sessions.IgnoreQueryFilters()
                    .Where(entity => entity.OwnerTrainerId == trainerId.Value),
                minimum: 2);
            await RequireCountAsync(
                "check-ins",
                context.CheckIns.IgnoreQueryFilters()
                    .Where(entity => entity.OwnerTrainerId == trainerId.Value),
                minimum: 2);
            await RequireCountAsync(
                "pack type",
                context.PackTypes.IgnoreQueryFilters()
                    .Where(entity =>
                        entity.OwnerTrainerId == trainerId.Value
                        && entity.Name == "Pack 10 sessões"),
                minimum: 1);
            await RequireCountAsync(
                "client session pack",
                context.ClientSessionPacks.IgnoreQueryFilters()
                    .Where(entity => entity.OwnerTrainerId == trainerId.Value),
                minimum: 1);
            await RequireCountAsync(
                "supplement assignment",
                context.ClientSupplementAssignments.IgnoreQueryFilters()
                    .Where(entity => entity.OwnerTrainerId == trainerId.Value),
                minimum: 1);
            await RequireCountAsync(
                "supplement intake",
                context.ClientSupplementIntakes.IgnoreQueryFilters()
                    .Where(entity => entity.OwnerTrainerId == trainerId.Value),
                minimum: 1);

            if (clientId.HasValue)
            {
                await RequireCountAsync(
                    "exercise set logs",
                    context.ClientExerciseSetLogs.IgnoreQueryFilters()
                        .Where(entity => entity.ClientId == clientId.Value),
                    minimum: 4);
            }
            else
            {
                missing.Add("client linked to portal user");
            }

            await RequireCountAsync(
                "workout completion",
                context.WorkoutCompletions.IgnoreQueryFilters()
                    .Where(entity => entity.OwnerTrainerId == trainerId.Value),
                minimum: 1);
        }

        await RequireCountAsync(
            "global exercises",
            context.Exercises.IgnoreQueryFilters()
                .Where(entity =>
                    entity.OwnerTrainerId == null
                    && (entity.Name == "Agachamento com barra" || entity.Name == "Supino plano")),
            minimum: 2);
        await RequireCountAsync(
            "global foods",
            context.Foods.IgnoreQueryFilters()
                .Where(entity =>
                    entity.OwnerTrainerId == null
                    && (entity.Name == "Peito de frango grelhado" || entity.Name == "Arroz branco cozido")),
            minimum: 2);
        await RequireCountAsync(
            "global supplements",
            context.Supplements.IgnoreQueryFilters()
                .Where(entity =>
                    entity.OwnerTrainerId == null
                    && entity.Name == "Creatina Monohidratada"),
            minimum: 1);
        await RequireCountAsync(
            "administrative audit entries",
            context.AdministrativeAuditEntries.IgnoreQueryFilters(),
            minimum: 5);

        if (missing.Count > 0)
            throw new InvalidOperationException(
                "Development seed is partial. Missing or invalid elements: "
                + string.Join(", ", missing));

        void RequireUser(string normalizedEmail, string role)
        {
            var user = users.SingleOrDefault(candidate =>
                candidate.NormalizedEmail == normalizedEmail);

            if (user is null)
            {
                missing.Add($"user {normalizedEmail}");
                return;
            }

            if (!user.EmailConfirmed || !string.Equals(user.Role, role, StringComparison.Ordinal))
                missing.Add($"confirmed {role} {normalizedEmail}");
        }

        async Task RequireCountAsync<TEntity>(
            string description,
            IQueryable<TEntity> query,
            int minimum)
            where TEntity : class
        {
            if (await query.CountAsync(cancellationToken) < minimum)
                missing.Add(description);
        }
    }

    /// <summary>
    /// Cria o superuser. Devolve <c>null</c> quando já existe; o chamador ainda tem de
    /// validar separadamente se o conjunto completo foi semeado.
    /// </summary>
    private async Task<Guid?> EnsureSuperuserAsync(DateTime now, CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        Establish(
            scope,
            trainerId: null,
            userId: null,
            role: "superuser",
            isAdministrative: true);

        var context = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();
        var email = new EmailAddress(_options.SuperuserEmail);

        var existing = await context.Users
            .AnyAsync(user => user.NormalizedEmail == email.Normalized, cancellationToken);

        if (existing)
            return null;

        var superuser = CreateAccount(
            scope,
            email,
            "superuser",
            "Administrator PT Manager",
            now);
        superuser.ConfirmEmail(now);
        context.Users.Add(superuser);
        await context.SaveChangesAsync(cancellationToken);

        return superuser.Id;
    }

    /// <summary>
    /// Semeia o catálogo global, que só existe em contexto administrativo.
    /// </summary>
    /// <remarks>
    /// Cada item traz a sua entrada de auditoria: o interceptor recusa escritas globais
    /// sem rasto de quem as fez, e é essa regra que impede um catálogo partilhado de ser
    /// alterado anonimamente.
    /// </remarks>
    private async Task<GlobalCatalog> SeedGlobalCatalogAsync(
        Guid superuserId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        Establish(
            scope,
            trainerId: null,
            userId: superuserId,
            role: "superuser",
            isAdministrative: true);

        var context = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();

        var squat = new Exercise(
            ownerTrainerId: null,
            name: "Agachamento com barra",
            description: "Agachamento livre, barra nas costas.",
            muscleGroups: "quadriceps,glutes",
            equipment: "Barra",
            difficultyLevel: "intermediate",
            videoUrl: null,
            now);

        var bench = new Exercise(
            ownerTrainerId: null,
            name: "Supino plano",
            description: "Supino com barra em banco plano.",
            muscleGroups: "chest,triceps",
            equipment: "Barra",
            difficultyLevel: "intermediate",
            videoUrl: null,
            now);

        var chicken = new Food(
            ownerTrainerId: null,
            name: "Peito de frango grelhado",
            description: "Valores por 100g.",
            protein: 31m,
            carbs: 0m,
            fats: 3.6m,
            fiber: 0m,
            now,
            defaultServingGrams: 150m);

        var rice = new Food(
            ownerTrainerId: null,
            name: "Arroz branco cozido",
            description: "Valores por 100g.",
            protein: 2.7m,
            carbs: 28m,
            fats: 0.3m,
            fiber: 0.4m,
            now,
            defaultServingGrams: 200m);

        var creatine = new Supplement(
            ownerTrainerId: null,
            createdByUserId: superuserId,
            name: "Creatina Monohidratada",
            description: "Suplemento de creatina.",
            unitOfMeasure: "g",
            servingSize: "5g",
            timing: "Após o treino",
            trainerNotes: null,
            now);

        context.Exercises.AddRange(squat, bench);
        context.Foods.AddRange(chicken, rice);
        context.Supplements.Add(creatine);

        context.AdministrativeAuditEntries.AddRange(
            Audit(superuserId, "seed_global_exercise", "exercise", squat.Id, squat.Name, now),
            Audit(superuserId, "seed_global_exercise", "exercise", bench.Id, bench.Name, now),
            Audit(superuserId, "seed_global_food", "food", chicken.Id, chicken.Name, now),
            Audit(superuserId, "seed_global_food", "food", rice.Id, rice.Name, now),
            Audit(superuserId, "seed_global_supplement", "supplement", creatine.Id, creatine.Name, now));

        await context.SaveChangesAsync(cancellationToken);

        return new GlobalCatalog(squat.Id, bench.Id, chicken.Id, rice.Id, creatine.Id);
    }

    /// <summary>Semeia o tenant do personal trainer com dados para todos os ecrãs.</summary>
    private async Task SeedTrainerTenantAsync(
        GlobalCatalog catalog,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(now);

        // 1. Conta, subscrição e definições do personal trainer.
        Guid trainerId;
        await using (var bootstrap = _scopeFactory.CreateAsyncScope())
        {
            Establish(
                bootstrap,
                trainerId: null,
                userId: null,
                role: "superuser",
                isAdministrative: true);
            var context = bootstrap.ServiceProvider.GetRequiredService<PtManagerDbContext>();

            var trainer = CreateAccount(
                bootstrap,
                new EmailAddress(_options.TrainerEmail),
                "trainer",
                "Leandro Alves",
                now);
            trainer.ConfirmEmail(now);
            context.Users.Add(trainer);
            await context.SaveChangesAsync(cancellationToken);

            trainerId = trainer.Id;
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        Establish(
            scope,
            trainerId,
            trainerId,
            "trainer",
            isAdministrative: false);
        var db = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();

        var subscription = new TrainerSubscription(trainerId, now.AddDays(30), now);
        var settings = new Domain.Entities.TrainerSettings.TrainerSettings(trainerId, now);
        db.TrainerSubscriptions.Add(subscription);
        db.TrainerSettings.Add(settings);

        // 2. Clientes. O primeiro tem conta ligada, para se poder entrar no portal.
        var clientUser = CreateAccount(
            scope,
            new EmailAddress(_options.ClientEmail),
            "client",
            "João Pereira",
            now);
        clientUser.ConfirmEmail(now);
        db.Users.Add(clientUser);

        // Telefones distintos: existe um índice único por tenant e telefone ativo.
        var joao = NewClient(
            trainerId,
            "João Pereira",
            "joao@ptmanager.local",
            "+351912345678",
            today,
            now);
        joao.AttachUser(clientUser.Id, now);
        var ana = NewClient(
            trainerId,
            "Ana Costa",
            "ana@ptmanager.local",
            "+351912345679",
            today,
            now);

        db.Clients.AddRange(joao, ana);
        subscription.RegisterClientAdded(now);
        subscription.RegisterClientAdded(now);

        // 3. Avaliação inicial do cliente com conta.
        db.InitialAssessments.Add(new InitialAssessment(
            trainerId,
            joao.Id,
            weightKg: 78.4m,
            heightCm: 178,
            bodyFatPercentage: 18.2m,
            medicalConditions: null,
            fitnessLevel: "intermediate",
            activityLevel: ActivityLevel.ModeratelyActive,
            goals: "Ganhar massa muscular e manter o peso estável.",
            profession: "Engenheiro",
            bodyMeasurements: new BodyMeasurements(84m, 98m, 102m, 36m, 36m, 58m, 58m, 38m, 38m),
            nutritionIntake: null,
            now));

        // 4. Catálogo privado do personal trainer, a par do global.
        var privateExercise = new Exercise(
            trainerId,
            "Remada curvada",
            "Variante do personal trainer.",
            "back,biceps",
            "Barra",
            "intermediate",
            null,
            now);
        var privateFood = new Food(
            trainerId,
            "Batido pós-treino",
            "Receita da casa.",
            24m,
            30m,
            5m,
            2m,
            now,
            300m);
        var privateSupplement = new Supplement(
            trainerId,
            trainerId,
            "Multivitamínico",
            null,
            "cápsula",
            "1 cápsula",
            "Ao pequeno almoço",
            "Comprar na farmácia.",
            now);

        db.Exercises.Add(privateExercise);
        db.Foods.Add(privateFood);
        db.Supplements.Add(privateSupplement);

        // 5. Plano de treino ativo, com RPE planeado nas séries.
        var plan = new TrainingPlan(
            trainerId,
            joao.Id,
            "Hipertrofia - bloco 1",
            "Três dias por semana.",
            "hypertrophy",
            null,
            today.AddDays(-14),
            today.AddDays(28),
            now);
        db.TrainingPlans.Add(plan);

        // O plano é um agregado: os dias, os exercícios e as séries entram pelos seus
        // métodos, não por `DbSet`. O interceptor recusa filhos órfãos, e com razão —
        // é assim que garante que ninguém liga uma série a um dia de outro plano.
        var monday = plan.AddDay(dayOfWeek: 1, weekNumber: 1, "Inferiores", now);
        var wednesday = plan.AddDay(dayOfWeek: 3, weekNumber: 1, "Superiores", now);

        var mondaySquat = monday.AddExercise(catalog.SquatId, 1, null, null, null, now);
        var wednesdayBench = wednesday.AddExercise(catalog.BenchId, 1, null, null, null, now);
        var wednesdayRow = wednesday.AddExercise(privateExercise.Id, 2, null, null, null, now);

        for (var setNumber = 1; setNumber <= 4; setNumber++)
        {
            mondaySquat.AddSet(
                setNumber,
                plannedReps: 8,
                plannedWeightKg: 80m,
                restSecondsMin: 90,
                restSecondsMax: 120,
                now,
                plannedRpe: 7.5m);
        }

        for (var setNumber = 1; setNumber <= 3; setNumber++)
        {
            wednesdayBench.AddSet(
                setNumber,
                plannedReps: 10,
                plannedWeightKg: 60m,
                restSecondsMin: 60,
                restSecondsMax: 90,
                now,
                plannedRpe: 8m);

            // RPE opcional: uma série sem RPE planeado tem de continuar válida.
            wednesdayRow.AddSet(
                setNumber,
                plannedReps: 12,
                plannedWeightKg: 45m,
                restSecondsMin: 60,
                restSecondsMax: 90,
                now,
                plannedRpe: null);
        }

        plan.Activate(now);

        // 6. Plano alimentar com uma refeição e dois itens.
        var macros = MacroTargetCalculator.CalculateFromPercentage(
            2400m, new PercentageMacroInput(30m, 40m, 30m));
        var mealPlan = new MealPlan(
            trainerId,
            joao.Id,
            "Manutenção - 2400 kcal",
            null,
            today.AddDays(-7),
            today.AddDays(21),
            NutritionCalculationSnapshot.FromManualEnergy(78.4m, macros, now),
            now);

        var lunch = mealPlan.AddMeal("Almoço", 1, now);
        lunch.AddItem(catalog.ChickenId, 180m, 1, now);
        lunch.AddItem(catalog.RiceId, 200m, 2, now);
        db.MealPlans.Add(mealPlan);

        // 7. Tipos de pack, pack do cliente e sessões.
        var packType = new PackType(
            trainerId,
            "Pack 10 sessões",
            10,
            30000,
            "EUR",
            90,
            now);
        db.PackTypes.Add(packType);

        var pack = new ClientSessionPack(
            trainerId, joao.Id, packType, today.AddDays(-30), today.AddDays(60), now);
        db.ClientSessionPacks.Add(pack);

        db.Sessions.AddRange(
            new Session(
                trainerId, joao.Id, pack.Id, new DateTimeOffset(now.Date.AddHours(10), TimeSpan.Zero),
                60, "Estúdio", "personal", null, now),
            new Session(
                trainerId, ana.Id, null, new DateTimeOffset(now.Date.AddDays(1).AddHours(18), TimeSpan.Zero),
                45, "Estúdio", "avaliação", null, now));

        // 8. Check-ins: um respondido por rever e um por responder hoje.
        var answered = new CheckIn(trainerId, joao.Id, today.AddDays(-3), today.AddDays(-3), now);
        answered.SubmitResponse(
            weightKg: 78.4m, bodyFatPercentage: 18m, notes: "Semana estável.",
            bodyMeasurements: null,
            feedback: new CheckInFeedback("Normal", "Normal", "Alta", "Boa", "Bons", "Positiva"),
            trainingAdherenceScore: 4, nutritionAdherenceScore: 3,
            localToday: today.AddDays(-3), now);

        var pending = new CheckIn(trainerId, ana.Id, today, today, now);
        db.CheckIns.AddRange(answered, pending);

        // 9. Suplemento atribuído, com a toma de hoje registada.
        var assignment = new ClientSupplementAssignment(
            trainerId, joao.Id, catalog.CreatineId, "5g", "Após o treino", null, now);
        db.ClientSupplementAssignments.Add(assignment);

        // Primeira gravação: tudo o que é estrutura. O interceptor valida a actividade do
        // cliente contra a base de dados, e não contra o change tracker, por isso o plano,
        // os dias e a atribuição têm de estar persistidos antes de haver séries ou tomas.
        await db.SaveChangesAsync(cancellationToken);

        // Segunda gravação: atividade do cliente. (Sessão de treino)
        var performedAt = new DateTimeOffset(now.AddDays(-2), TimeSpan.Zero);
        for (var setNumber = 1; setNumber <= 4; setNumber++)
        {
            db.ClientExerciseSetLogs.Add(new ClientExerciseSetLog(
                joao.Id, mondaySquat.Id, setNumber, weightKg: 80m, repsDone: 8,
                notes: null, performedAt, now, rpe: 8m));
        }

        db.WorkoutCompletions.Add(new WorkoutCompletion(
            trainerId, joao.Id, plan.Id, monday.Id, today.AddDays(-2), "Correu bem", now));

        db.ClientSupplementIntakes.Add(new ClientSupplementIntake(
            trainerId, joao.Id, assignment.Id, today, now));

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Entrada de auditoria de uma criação global.
    /// </summary>
    /// <remarks>
    /// O estado anterior é nulo porque o recurso não existia; o posterior tem de existir,
    /// exigência do próprio domínio; uma auditoria sem nenhum dos dois não regista nada.
    /// </remarks>
    private static AdministrativeAuditEntry Audit(
        Guid actorUserId,
        string action,
        string resourceType,
        Guid resourceId,
        string createdName,
        DateTime now) =>
        new(
            actorUserId,
            action,
            resourceType,
            resourceId,
            beforeState: null,
            afterState: $"{{ \"name\": \"{createdName}\" }}",
            now);

    private static Client NewClient(
        Guid trainerId,
        string name,
        string contactEmail,
        string phone,
        DateOnly today,
        DateTime now) =>
        new(
            trainerId,
            name,
            contactEmail,
            phone,
            BirthDate.Create(new DateOnly(1992, 4, 17), today),
            BiologicalSex.Male,
            objective: "Hipertrofia",
            notes: null,
            emergencyContactName: null,
            emergencyContactPhone: null,
            now);

    /// <summary>Cria uma conta com a password de desenvolvimento já aplicada.</summary>
    private User CreateAccount(
        IServiceScope scope,
        EmailAddress email,
        string role,
        string fullName,
        DateTime now)
    {
        var user = new User(email, role, fullName, now);
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        user.SetPasswordHash(hasher.HashPassword(user, _options.Password), now);
        return user;
    }

    private static void Establish(
        IServiceScope scope,
        Guid? trainerId,
        Guid? userId,
        string role,
        bool isAdministrative) =>
        scope.ServiceProvider
            .GetRequiredService<ITenantContextInitializer>()
            .Establish(trainerId, userId, role, TenantOrigin.System, isAdministrative);

    /// <summary>Identificadores do catálogo global semeado.</summary>
    private sealed record GlobalCatalog(
        Guid SquatId,
        Guid BenchId,
        Guid ChickenId,
        Guid RiceId,
        Guid CreatineId);
}
