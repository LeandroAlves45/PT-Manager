using Application.Common.Abstractions;
using Domain.Entities.Administration;
using Domain.Entities.Assessments;
using Domain.Entities.Billing;
using Domain.Entities.Clients;
using Domain.Entities.Identity;
using Domain.Entities.Jobs;
using Domain.Entities.Notifications;
using Domain.Entities.Nutrition;
using Domain.Entities.Sessions;
using Domain.Entities.Supplements;
using Domain.Entities.TrainerSettings;
using Domain.Entities.Training;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data;

/// <summary>
/// DbContext do PtManager. Aplica Global Query Filters multi-tenant e valida as
/// escritas tenant-owned antes de as persistir.
/// </summary>
public sealed class PtManagerDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    /// <summary>
    /// Personal Trainer efetivo deste instância. Propriedade de INSTÂNCIA, lida pelas
    /// Global Query Filters.
    /// </summary>
    private Guid? CurrentTrainerId => _tenantContext.TrainerId;

    public PtManagerDbContext(
        DbContextOptions<PtManagerDbContext> options,
        ITenantContext tenantContext
    ) : base(options)
    {
        _tenantContext = tenantContext;
    }

    // DbSet<T> para as 29 entidades.
    public DbSet<User> Users => Set<User>();
    public DbSet<AdministrativeAuditEntry> AdministrativeAuditEntries =>
        Set<AdministrativeAuditEntry>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<InitialAssessment> InitialAssessments => Set<InitialAssessment>();
    public DbSet<CheckIn> CheckIns => Set<CheckIn>();
    public DbSet<Exercise> Exercises => Set<Exercise>();
    public DbSet<ClientSessionPack> ClientSessionPacks => Set<ClientSessionPack>();
    public DbSet<PackType> PackTypes => Set<PackType>();
    public DbSet<ProcessedStripeEvent> ProcessedStripeEvents => Set<ProcessedStripeEvent>();
    public DbSet<TrainerSubscription> TrainerSubscriptions => Set<TrainerSubscription>();
    public DbSet<InviteToken> InviteTokens => Set<InviteToken>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<DurableJob> DurableJobs => Set<DurableJob>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Food> Foods => Set<Food>();
    public DbSet<MealPlan> MealPlans => Set<MealPlan>();
    public DbSet<MealPlanMealItem> MealPlanMealItems => Set<MealPlanMealItem>();
    public DbSet<MealPlanMeal> MealPlanMeals => Set<MealPlanMeal>();
    public DbSet<MealPlanMealSupplement> MealPlanMealSupplements => Set<MealPlanMealSupplement>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<Supplement> Supplements => Set<Supplement>();
    public DbSet<ClientSupplementAssignment> ClientSupplementAssignments => Set<ClientSupplementAssignment>();
    public DbSet<TrainerSettings> TrainerSettings => Set<TrainerSettings>();
    public DbSet<ClientExerciseSetLog> ClientExerciseSetLogs => Set<ClientExerciseSetLog>();
    public DbSet<ExerciseSet> ExerciseSets => Set<ExerciseSet>();
    public DbSet<TrainingPlan> TrainingPlans => Set<TrainingPlan>();
    public DbSet<TrainingPlanDay> TrainingPlanDays => Set<TrainingPlanDay>();
    public DbSet<TrainingPlanDayExercise> TrainingPlanDayExercises => Set<TrainingPlanDayExercise>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {

        // Carrega todas as IEntityTypeConfiguration<T> do assembly de uma vez.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PtManagerDbContext).Assembly);

        // As IEntityTypeConfiguration<T> são criadas sem acesso seguro à
        // instância deste DbContext. Os filtros ficam aqui para capturarem
        // CurrentTrainerId como parâmetro por instância e nunca como valor do
        // primeiro pedido guardado no modelo em cache.
        ApplyGlobalQueryFilters(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }

    /// <summary>
    /// Aplica os Global Query Filters multi-tenant. Ponto único de configuração de segurança
    /// de leitura -> todas as politicas vivem aqui, nunca dentro de uma EntityTypeConfiguration
    /// individual.
    /// </summary>
    private void ApplyGlobalQueryFilters(ModelBuilder modelBuilder)
    {
        // POLÍTICA A — tenant-owned estrito.
        // OwnerTrainerId é Guid (não-nullable) + IsDeleted bool.
        modelBuilder.Entity<Client>().HasQueryFilter(c =>
            CurrentTrainerId.HasValue && !c.IsDeleted && c.OwnerTrainerId == CurrentTrainerId);

        modelBuilder.Entity<MealPlan>().HasQueryFilter(mp =>
            CurrentTrainerId.HasValue && !mp.IsDeleted && mp.OwnerTrainerId == CurrentTrainerId);

        modelBuilder.Entity<TrainingPlan>().HasQueryFilter(tp =>
            CurrentTrainerId.HasValue && !tp.IsDeleted && tp.OwnerTrainerId == CurrentTrainerId);

        modelBuilder.Entity<Session>().HasQueryFilter(s =>
            CurrentTrainerId.HasValue && !s.IsDeleted && s.OwnerTrainerId == CurrentTrainerId);

        modelBuilder.Entity<InitialAssessment>().HasQueryFilter(ia =>
            CurrentTrainerId.HasValue && !ia.IsDeleted && ia.OwnerTrainerId == CurrentTrainerId);

        modelBuilder.Entity<CheckIn>().HasQueryFilter(ci =>
            CurrentTrainerId.HasValue && !ci.IsDeleted && ci.OwnerTrainerId == CurrentTrainerId);

        modelBuilder.Entity<ClientSessionPack>().HasQueryFilter(csp =>
            CurrentTrainerId.HasValue && !csp.IsDeleted && csp.OwnerTrainerId == CurrentTrainerId);

        modelBuilder.Entity<Notification>().HasQueryFilter(n =>
            CurrentTrainerId.HasValue && !n.IsDeleted && n.OwnerTrainerId == CurrentTrainerId);

        modelBuilder.Entity<PackType>().HasQueryFilter(pt =>
            CurrentTrainerId.HasValue && !pt.IsDeleted && pt.OwnerTrainerId == CurrentTrainerId);

        // POLÍTICA A' — tenant-owned sem soft delete, propriedade chama-se
        // TrainerId (não OwnerTrainerId).
        modelBuilder.Entity<TrainerSettings>().HasQueryFilter(ts =>
            CurrentTrainerId.HasValue && ts.TrainerId == CurrentTrainerId);

        modelBuilder.Entity<TrainerSubscription>().HasQueryFilter(ts =>
            CurrentTrainerId.HasValue && ts.TrainerId == CurrentTrainerId);

        // POLÍTICA B — catálogo com linhas globais. OwnerTrainerId é
        // Guid? (nullable) + IsDeleted bool.
        modelBuilder.Entity<Food>().HasQueryFilter(f =>
            CurrentTrainerId.HasValue &&
            (f.OwnerTrainerId == null || f.OwnerTrainerId == CurrentTrainerId));

        modelBuilder.Entity<Exercise>().HasQueryFilter(e =>
            CurrentTrainerId.HasValue &&
            (e.OwnerTrainerId == null || e.OwnerTrainerId == CurrentTrainerId));

        modelBuilder.Entity<Supplement>().HasQueryFilter(s =>
            CurrentTrainerId.HasValue &&
            (s.OwnerTrainerId == null || s.OwnerTrainerId == CurrentTrainerId));

        modelBuilder.Entity<ClientSupplementAssignment>().HasQueryFilter(assignment =>
            CurrentTrainerId.HasValue &&
            assignment.OwnerTrainerId == CurrentTrainerId);

        // POLÍTICA A DERIVADA — filhas de agregado, SEM navegação POCO.
        modelBuilder.Entity<MealPlanMeal>().HasQueryFilter(mpm =>
            CurrentTrainerId.HasValue &&
            Set<MealPlan>().Any(mp =>
                mp.Id == mpm.MealPlanId &&
                !mp.IsDeleted &&
                mp.OwnerTrainerId == CurrentTrainerId));

        modelBuilder.Entity<MealPlanMealItem>().HasQueryFilter(mpmi =>
            CurrentTrainerId.HasValue &&
            Set<MealPlanMeal>().Any(mpm =>
                mpm.Id == mpmi.MealPlanMealId &&
                Set<MealPlan>().Any(mp =>
                    mp.Id == mpm.MealPlanId &&
                    !mp.IsDeleted &&
                    mp.OwnerTrainerId == CurrentTrainerId)));

        modelBuilder.Entity<MealPlanMealSupplement>().HasQueryFilter(mpms =>
            CurrentTrainerId.HasValue &&
            Set<MealPlanMeal>().Any(mpm =>
                mpm.Id == mpms.MealPlanMealId &&
                Set<MealPlan>().Any(mp =>
                    mp.Id == mpm.MealPlanId &&
                    !mp.IsDeleted &&
                    mp.OwnerTrainerId == CurrentTrainerId)));

        modelBuilder.Entity<TrainingPlanDay>().HasQueryFilter(tpd =>
            CurrentTrainerId.HasValue &&
            Set<TrainingPlan>().Any(tp =>
                tp.Id == tpd.TrainingPlanId &&
                !tp.IsDeleted &&
                tp.OwnerTrainerId == CurrentTrainerId));

        modelBuilder.Entity<TrainingPlanDayExercise>().HasQueryFilter(tpde =>
            CurrentTrainerId.HasValue &&
            Set<TrainingPlanDay>().Any(tpd =>
                tpd.Id == tpde.TrainingPlanDayId &&
                Set<TrainingPlan>().Any(tp =>
                    tp.Id == tpd.TrainingPlanId &&
                    !tp.IsDeleted &&
                    tp.OwnerTrainerId == CurrentTrainerId)));

        modelBuilder.Entity<ExerciseSet>().HasQueryFilter(es =>
            CurrentTrainerId.HasValue &&
            Set<TrainingPlanDayExercise>().Any(tpde =>
                tpde.Id == es.TrainingPlanDayExerciseId &&
                Set<TrainingPlanDay>().Any(tpd =>
                    tpd.Id == tpde.TrainingPlanDayId &&
                    Set<TrainingPlan>().Any(tp =>
                        tp.Id == tpd.TrainingPlanId &&
                        !tp.IsDeleted &&
                        tp.OwnerTrainerId == CurrentTrainerId))));

        modelBuilder.Entity<ClientExerciseSetLog>().HasQueryFilter(cesl =>
            CurrentTrainerId.HasValue &&
            Set<TrainingPlanDayExercise>().Any(tpde =>
                tpde.Id == cesl.TrainingPlanDayExerciseId &&
                Set<TrainingPlanDay>().Any(tpd =>
                    tpd.Id == tpde.TrainingPlanDayId &&
                    Set<TrainingPlan>().Any(tp =>
                        tp.Id == tpd.TrainingPlanId &&
                        !tp.IsDeleted &&
                        tp.OwnerTrainerId == CurrentTrainerId))));

        // POLÍTICA C — sem filtro de tenant.
        // User, RefreshToken, InviteToken, ProcessedStripeEvent: raiz do
        // tenant ou identidade externa — filtrá-los impediria o próprio
        // login de encontrar a conta.
    }

    /// <summary>
    /// Personal trainer efetivo, garantido não nulo. Lnaçar aqui é preferível a devolver
    /// null. Usado pelo TenantWriteValidationInterceptor antes de validar escritas.
    /// </summary>
    internal Guid RequireTenant() =>
        _tenantContext.TrainerId
        ?? throw new InvalidOperationException(
            "No effective tenant is established for this operation."
        );
}


