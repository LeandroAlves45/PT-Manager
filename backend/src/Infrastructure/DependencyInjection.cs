using Application.Common.Abstractions;
using Application.Features.Administration.ContentModeration.Abstractions;
using Application.Features.Assessments.CheckIns.Abstractions;
using Application.Features.Assessments.InitialAssessments.Abstractions;
using Application.Features.ClientPortal.Abstractions;
using Application.Features.Clients.Abstractions;
using Application.Features.Jobs.Abstractions;
using Application.Features.Nutrition.Foods.Abstractions;
using Application.Features.Nutrition.MealPlans.Abstractions;
using Application.Features.Notifications.Abstractions;
using Application.Features.Packs.ClientSessionPacks.Abstractions;
using Application.Features.Packs.PackTypes.Abstractions;
using Application.Features.Sessions.Abstractions;
using Application.Features.Supplements.Abstractions;
using Application.Features.TrainerSettings.Abstractions;
using Application.Features.Training.Exercises.Abstractions;
using Application.Features.Training.ExerciseSetLogs.Abstractions;
using Application.Features.Training.TrainingPlans.Abstractions;
using Infrastructure.Data;
using Infrastructure.Data.Interceptors;
using Infrastructure.Identity;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Administration;
using Infrastructure.Persistence.Assessments;
using Infrastructure.Persistence.Billing;
using Infrastructure.Persistence.ClientPortal;
using Infrastructure.Persistence.Clients;
using Infrastructure.Persistence.Errors;
using Infrastructure.Persistence.Nutrition;
using Infrastructure.Persistence.Notifications;
using Infrastructure.Persistence.Packs;
using Infrastructure.Persistence.Sessions;
using Infrastructure.Persistence.Supplements;
using Infrastructure.Persistence.Training;
using Infrastructure.Persistence.TrainerSettings;
using Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace Infrastructure;

/// <summary>
/// Regista Infrastructure e adapters específicos por feature.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' is not configured."
            );

        // Regista o DbContext com a connection string
        services.AddDbContext<PtManagerDbContext>((provider, options) =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(PtManagerDbContext).Assembly.FullName);
                npgsql.EnableRetryOnFailure(maxRetryCount: 3);
            });

            options.AddInterceptors(provider.GetRequiredService<TenantWriteValidationInterceptor>());
        });

        // Scoped: o tenant é do pedido. Registado pelas duas interfaces para que o
        // middleware possa chamar Establish e o resto do código possa ler.
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(provider =>
            provider.GetRequiredService<TenantContext>());
        services.AddScoped<ITenantContextInitializer>(provider =>
            provider.GetRequiredService<TenantContext>());

        // Assessments
        services.AddScoped<IInitialAssessmentStore, InitialAssessmentStore>();
        services.AddScoped<IInitialAssessmentQueries, InitialAssessmentQueries>();
        services.AddScoped<ICheckInStore, CheckInStore>();
        services.AddScoped<ICheckInQueries, CheckInQueries>();

        // Clients
        services.AddScoped<IClientStore, ClientStore>();
        services.AddScoped<IClientQueries, ClientQueries>();
        services.AddScoped<IClientBrandingQueries, ClientBrandingQueries>();

        // Client Portal
        services.AddScoped<IMyTrainingPlanQueries, MyTrainingPlanQueries>();
        services.AddScoped<IMyNutritionPlanQueries, MyNutritionPlanQueries>();
        services.AddScoped<IMyProfileQueries, MyProfileQueries>();
        services.AddScoped<IMyProfileStore, MyProfileStore>();

        // Administrative content moderation
        services.AddScoped<IPrivateCatalogModerationStore, PrivateCatalogModerationStore>();

        // Foods
        services.AddScoped<IFoodStore, FoodStore>();
        services.AddScoped<IFoodQueries, FoodQueries>();
        services.AddScoped<IGlobalFoodStore, GlobalFoodStore>();
        services.AddScoped<IGlobalFoodQueries, GlobalFoodQueries>();

        // Meal Plans
        services.AddScoped<IMealPlanStore, MealPlanStore>();
        services.AddScoped<IMealPlanQueries, MealPlanQueries>();

        // Exercises
        services.AddScoped<IExerciseStore, ExerciseStore>();
        services.AddScoped<IExerciseQueries, ExerciseQueries>();
        services.AddScoped<IGlobalExerciseStore, GlobalExerciseStore>();
        services.AddScoped<IGlobalExerciseQueries, GlobalExerciseQueries>();

        // Training Plans
        services.AddScoped<ITrainingPlanStore, TrainingPlanStore>();
        services.AddScoped<ITrainingPlanQueries, TrainingPlanQueries>();

        // Exercise Set Logs
        services.AddScoped<IExerciseSetLogStore, ExerciseSetLogStore>();
        services.AddScoped<IExerciseSetLogQueries, ExerciseSetLogQueries>();

        // Training Plan Structure Coordinator
        services.AddScoped<TrainingPlanStructureCoordinator>();

        // Supplements
        services.AddScoped<ISupplementStore, SupplementStore>();
        services.AddScoped<ISupplementQueries, SupplementQueries>();
        services.AddScoped<IClientSupplementAssignmentStore, ClientSupplementAssignmentStore>();
        services.AddScoped<IClientSupplementAssignmentQueries, ClientSupplementAssignmentQueries>();
        services.AddScoped<IGlobalSupplementStore, GlobalSupplementStore>();
        services.AddScoped<IGlobalSupplementQueries, GlobalSupplementQueries>();

        // Pack Types
        services.AddScoped<IPackTypeStore, PackTypeStore>();
        services.AddScoped<IPackTypeQueries, PackTypeQueries>();

        // Client Session Packs
        services.AddScoped<IClientSessionPackStore, ClientSessionPackStore>();
        services.AddScoped<IClientSessionPackQueries, ClientSessionPackQueries>();

        // Sessions
        services.AddScoped<ISessionStore, SessionStore>();
        services.AddScoped<ISessionQueries, SessionQueries>();

        // Trainer Settings
        services.AddScoped<ITrainerSettingsStore, TrainerSettingsStore>();
        services.AddScoped<ITrainerSettingsQueries, TrainerSettingsQueries>();

        // Notifications
        services.AddScoped<INotificationQueueStore, NotificationQueueStore>();

        // Interceptors
        services.AddScoped<TenantWriteValidationInterceptor>();

        // Errors and Clock
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<PostgresConstraintTranslator>();

        // Authentication Extensions
        services.AddAuthenticationInfrastructure(configuration);

        // Billing Extensions
        services.AddBillingInfrastructure();

        // Timezone provider
        services.AddScoped<ITrainerTimeZoneProvider, TrainerTimeZoneProvider>();

        // Jobs
        services.AddScoped<IDurableJobStore, DurableJobRepository>();
        services.AddScoped<IOutboxStore, OutboxRepository>();

        return services;
    }
}
