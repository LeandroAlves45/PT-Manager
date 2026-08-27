using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Application;

/// <summary>Regista explicitamente os handlers e validators da camada Application</summary>
public static class DependencyInjection
{
    /// <summary>Adiciona todos os use cases.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        AddAssessments(services);
        AddAuthentication(services);
        AddBilling(services);
        AddClients(services);
        AddNotifications(services);
        AddNutrition(services);
        AddPacks(services);
        AddSessions(services);
        AddSupplements(services);
        AddTrainerSettings(services);
        AddTraining(services);

        return services;
    }

    private static void AddAssessments(IServiceCollection services)
    {
        services.AddScoped<Application.Features.Assessments
            .CheckIns.CancelCheckIn.CancelCheckInHandler>();
        services.AddScoped<Application.Features.Assessments
            .CheckIns.CorrectCheckIn.CorrectCheckInHandler>();
        services.AddScoped<Application.Features.Assessments
            .CheckIns.CreateCheckIn.CreateCheckInHandler>();
        services.AddScoped<Application.Features.Assessments
            .CheckIns.GetCheckIn.GetCheckInHandler>();
        services.AddScoped<Application.Features.Assessments
            .CheckIns.GetMyDueCheckIn.GetMyDueCheckInHandler>();
        services.AddScoped<Application.Features.Assessments
            .CheckIns.ListCheckIns.ListCheckInsHandler>();
        services.AddScoped<Application.Features.Assessments
            .CheckIns.RescheduleCheckIn.RescheduleCheckInHandler>();
        services.AddScoped<Application.Features.Assessments
            .CheckIns.SubmitCheckInResponse.SubmitCheckInResponseHandler>();
        services.AddScoped<Application.Features.Assessments
            .InitialAssessments.CreateInitialAssessment.CreateInitialAssessmentHandler>();
        services.AddScoped<Application.Features.Assessments
            .InitialAssessments.GetInitialAssessment.GetInitialAssessmentHandler>();
        services.AddScoped<Application.Features.Assessments.
            InitialAssessments.UpdateInitialAssessment.UpdateInitialAssessmentHandler>();

        // Validators
        services.AddScoped<IValidator<Application.Features.Assessments
            .CheckIns.CorrectCheckIn.CorrectCheckInCommand>, Application.Features.Assessments
                .CheckIns.CorrectCheckIn.CorrectCheckInCommandValidator>();
        services.AddScoped<IValidator<Application.Features.Assessments.CheckIns.
            CreateCheckIn.CreateCheckInCommand>, Application.Features.Assessments.CheckIns.
                CreateCheckIn.CreateCheckInCommandValidator>();
        services.AddScoped<IValidator<Application.Features.Assessments.CheckIns.
            ListCheckIns.ListCheckInsQuery>, Application.Features.Assessments.CheckIns
                .ListCheckIns.ListCheckInsQueryValidator>();
        services.AddScoped<IValidator<Application.Features.Assessments.CheckIns
            .RescheduleCheckIn.RescheduleCheckInCommand>, Application.Features.Assessments.CheckIns
                .RescheduleCheckIn.RescheduleCheckInCommandValidator>();
        services.AddScoped<IValidator<Application.Features.Assessments.CheckIns
            .SubmitCheckInResponse.SubmitCheckInResponseCommand>, Application.Features.Assessments
                .CheckIns.SubmitCheckInResponse.SubmitCheckInResponseCommandValidator>();
        services.AddScoped<IValidator<Application.Features.Assessments.InitialAssessments
            .CreateInitialAssessment.CreateInitialAssessmentCommand>, Application.Features
                .Assessments.InitialAssessments.CreateInitialAssessment.CreateInitialAssessmentCommandValidator>();
        services.AddScoped<IValidator<Application.Features.Assessments.InitialAssessments
            .UpdateInitialAssessment.UpdateInitialAssessmentCommand>, Application.Features
                .Assessments.InitialAssessments.UpdateInitialAssessment.UpdateInitialAssessmentCommandValidator>();
    }

    private static void AddAuthentication(IServiceCollection services)
    {
        services.AddScoped<Application.Features.Authentication.ChangePassword.ChangePasswordHandler>();
        services.AddScoped<Application.Features.Authentication.ConfirmEmail.ConfirmEmailHandler>();
        services.AddScoped<Application.Features.Authentication.Logout.LogoutHandler>();
        services.AddScoped<Application.Features.Authentication.ResetPassword.ResetPasswordHandler>();

        // Validators
        services.AddScoped<IValidator<Application.Features.Authentication
            .AcceptClientInvite.AcceptClientInviteCommand>, Application.Features.Authentication.AcceptClientInvite.AcceptClientInviteCommandValidator>();
        services.AddScoped<IValidator<Application.Features.Authentication
            .ChangePassword.ChangePasswordCommand>, Application.Features.Authentication.ChangePassword.ChangePasswordCommandValidator>();
        services.AddScoped<IValidator<Application.Features.Authentication
            .ConfirmEmail.ConfirmEmailCommand>, Application.Features.Authentication.ConfirmEmail.ConfirmEmailCommandValidator>();
        services.AddScoped<IValidator<Application.Features.Authentication
            .InviteClient.InviteClientCommand>, Application.Features.Authentication.InviteClient.InviteClientCommandValidator>();
        services.AddScoped<IValidator<Application.Features.Authentication
            .Login.LoginCommand>, Application.Features.Authentication.Login.LoginCommandValidator>();
        services.AddScoped<IValidator<Application.Features.Authentication
            .Logout.LogoutCommand>, Application.Features.Authentication.Logout.LogoutCommandValidator>();
        services.AddScoped<IValidator<Application.Features.Authentication
            .RefreshSession.RefreshSessionCommand>, Application.Features.Authentication.RefreshSession.RefreshSessionCommandValidator>();
        services.AddScoped<IValidator<Application.Features.Authentication
            .RegisterTrainer.RegisterTrainerCommand>, Application.Features.Authentication.RegisterTrainer.RegisterTrainerCommandValidator>();
        services.AddScoped<IValidator<Application.Features.Authentication
            .RequestPasswordReset.RequestPasswordResetCommand>, Application.Features.Authentication.RequestPasswordReset.RequestPasswordResetCommandValidator>();
        services.AddScoped<IValidator<Application.Features.Authentication
            .ResetPassword.ResetPasswordCommand>, Application.Features.Authentication.ResetPassword.ResetPasswordCommandValidator>();
    }

    private static void AddBilling(IServiceCollection services)
    {
        services.AddScoped<Application.Features.Billing.GetSubscription.GetSubscriptionHandler>();

        // Validators
        services.AddScoped<IValidator<Application.Features.Billing
            .CreateCheckout.CreateCheckoutCommand>, Application.Features.Billing.CreateCheckout.CreateCheckoutCommandValidator>();
        services.AddScoped<IValidator<Application.Features.Billing
            .CreateCustomerPortal.CreateCustomerPortalCommand>, Application.Features.Billing.CreateCustomerPortal.CreateCustomerPortalCommandValidator>();
    }

    private static void AddClients(IServiceCollection services)
    {
        services.AddScoped<Application.Features.Clients.ArchiveClient.ArchiveClientHandler>();
        services.AddScoped<Application.Features.Clients.CreateClient.CreateClientHandler>();
        services.AddScoped<Application.Features.Clients.GetClient.GetClientHandler>();
        services.AddScoped<Application.Features.Clients.GetClientBranding.GetClientBrandingHandler>();
        services.AddScoped<Application.Features.Clients.ListClients.ListClientsHandler>();
        services.AddScoped<Application.Features.Clients.ReactivateClient.ReactivateClientHandler>();
        services.AddScoped<Application.Features.Clients.UpdateClient.UpdateClientHandler>();

        // Validators
        services.AddScoped<IValidator<Application.Features.Clients
            .CreateClient.CreateClientCommand>, Application.Features.Clients.CreateClient.CreateClientCommandValidator>();
        services.AddScoped<IValidator<Application.Features.Clients
            .ListClients.ListClientsQuery>, Application.Features.Clients.ListClients.ListClientsQueryValidator>();
        services.AddScoped<IValidator<Application.Features.Clients
            .UpdateClient.UpdateClientCommand>, Application.Features.Clients.UpdateClient.UpdateClientCommandValidator>();
    }

    private static void AddNotifications(IServiceCollection services)
    {
        services.AddScoped<Application.Features.Notifications.EnqueueNotification.EnqueueNotificationHandler>();

        // Validator
        services.AddScoped<IValidator<Application.Features.Notifications
            .EnqueueNotification.EnqueueNotificationCommand>, Application.Features.Notifications
                .EnqueueNotification.EnqueueNotificationCommandValidator>();
    }

    private static void AddNutrition(IServiceCollection services)
    {
        services.AddScoped<Application.Features.Nutrition.Foods.ArchiveFood.ArchiveFoodHandler>();
        services.AddScoped<Application.Features.Nutrition.Foods.ArchiveGlobalFood.ArchiveGlobalFoodHandler>();
        services.AddScoped<Application.Features.Nutrition.Foods.CreateFood.CreateFoodHandler>();
        services.AddScoped<Application.Features.Nutrition.Foods.CreateGlobalFood.CreateGlobalFoodHandler>();
        services.AddScoped<Application.Features.Nutrition.Foods.DeleteGlobalFood.DeleteGlobalFoodHandler>();
        services.AddScoped<Application.Features.Nutrition.Foods.GetFood.GetFoodHandler>();
        services.AddScoped<Application.Features.Nutrition.Foods.GetGlobalFood.GetGlobalFoodHandler>();
        services.AddScoped<Application.Features.Nutrition.Foods.ListFoods.ListFoodsHandler>();
        services.AddScoped<Application.Features.Nutrition.Foods.ListGlobalFoods.ListGlobalFoodsHandler>();
        services.AddScoped<Application.Features.Nutrition.Foods.ReactivateFood.ReactivateFoodHandler>();
        services.AddScoped<Application.Features.Nutrition.Foods.ReactivateGlobalFood.ReactivateGlobalFoodHandler>();
        services.AddScoped<Application.Features.Nutrition.Foods.UpdateFood.UpdateFoodHandler>();
        services.AddScoped<Application.Features.Nutrition.Foods.UpdateGlobalFood.UpdateGlobalFoodHandler>();
        services.AddScoped<Application.Features.Nutrition.MealPlans.ArchiveMealPlan.ArchiveMealPlanHandler>();
        services.AddScoped<Application.Features.Nutrition.MealPlans.CreateMealPlan.CreateMealPlanHandler>();
        services.AddScoped<Application.Features.Nutrition.MealPlans.GetMealPlan.GetMealPlanHandler>();
        services.AddScoped<Application.Features.Nutrition.MealPlans.ListMealPlans.ListMealPlansHandler>();
        services.AddScoped<Application.Features.Nutrition.MealPlans.ReactivateMealPlan.ReactivateMealPlanHandler>();
        services.AddScoped<Application.Features.Nutrition.MealPlans.UpdateMealPlan.UpdateMealPlanHandler>();
        services.AddScoped<Application.Features.Nutrition.PreviewNutrition.PreviewNutritionHandler>();

        // Validators
        services.AddScoped<IValidator<Application.Features.Nutrition.Foods
            .CreateFood.CreateFoodCommand>, Application.Features.Nutrition.Foods
                .CreateFood.CreateFoodCommandValidator>();
        services.AddScoped<IValidator<Application.Features.Nutrition.Foods
            .CreateGlobalFood.CreateGlobalFoodCommand>, Application.Features.Nutrition.Foods
                .CreateGlobalFood.CreateGlobalFoodCommandValidator>();
        services.AddScoped<IValidator<Application.Features.Nutrition.Foods
            .ListFoods.ListFoodsQuery>, Application.Features.Nutrition.Foods
                .ListFoods.ListFoodsQueryValidator>();
        services.AddScoped<IValidator<Application.Features.Nutrition.Foods
            .ListGlobalFoods.ListGlobalFoodsQuery>, Application.Features.Nutrition.Foods
                .ListGlobalFoods.ListGlobalFoodsQueryValidator>();
        services.AddScoped<IValidator<Application.Features.Nutrition.Foods
            .UpdateFood.UpdateFoodCommand>, Application.Features.Nutrition.Foods
                .UpdateFood.UpdateFoodCommandValidator>();
        services.AddScoped<IValidator<Application.Features.Nutrition.Foods
            .UpdateGlobalFood.UpdateGlobalFoodCommand>, Application.Features.Nutrition.Foods
                .UpdateGlobalFood.UpdateGlobalFoodCommandValidator>();
        services.AddScoped<IValidator<Application.Features.Nutrition.MealPlans
            .CreateMealPlan.CreateMealPlanCommand>, Application.Features.Nutrition.MealPlans
                .CreateMealPlan.CreateMealPlanCommandValidator>();
        services.AddScoped<IValidator<Application.Features.Nutrition.MealPlans
            .ListMealPlans.ListMealPlansQuery>, Application.Features.Nutrition.MealPlans
                .ListMealPlans.ListMealPlansQueryValidator>();
        services.AddScoped<IValidator<Application.Features.Nutrition.MealPlans
            .UpdateMealPlan.UpdateMealPlanCommand>, Application.Features.Nutrition.MealPlans
                .UpdateMealPlan.UpdateMealPlanCommandValidator>();
        services.AddScoped<IValidator<Application.Features.Nutrition
            .PreviewNutrition.PreviewNutritionCommand>, Application.Features.Nutrition
                .PreviewNutrition.PreviewNutritionCommandValidator>();
    }

    private static void AddPacks(IServiceCollection services)
    {
        services.AddScoped<Application.Features.Packs.ClientSessionPacks
            .AssignClientSessionPack.AssignClientSessionPackHandler>();
        services.AddScoped<Application.Features.Packs.ClientSessionPacks
            .CancelClientSessionPack.CancelClientSessionPackHandler>();
        services.AddScoped<Application.Features.Packs.ClientSessionPacks
            .GetClientSessionPack.GetClientSessionPackHandler>();
        services.AddScoped<Application.Features.Packs.ClientSessionPacks
            .ListClientSessionPacks.ListClientSessionPacksHandler>();
        services.AddScoped<Application.Features.Packs.ClientSessionPacks
            .ListUsableClientSessionPacks.ListUsableClientSessionPacksHandler>();
        services.AddScoped<Application.Features.Packs.ClientSessionPacks
            .UpdateClientSessionPackExpectedEndDate.UpdateClientSessionPackExpectedEndDateHandler>();
        services.AddScoped<Application.Features.Packs.PackTypes
            .ArchivePackType.ArchivePackTypeHandler>();
        services.AddScoped<Application.Features.Packs.PackTypes
            .CreatePackType.CreatePackTypeHandler>();
        services.AddScoped<Application.Features.Packs.PackTypes
            .GetPackType.GetPackTypeHandler>();
        services.AddScoped<Application.Features.Packs.PackTypes
            .ListPackTypes.ListPackTypesHandler>();
        services.AddScoped<Application.Features.Packs.PackTypes
            .ReactivatePackType.ReactivatePackTypeHandler>();
        services.AddScoped<Application.Features.Packs.PackTypes
            .UpdatePackType.UpdatePackTypeHandler>();

        // Validators
        services.AddScoped<IValidator<Application.Features.Packs.ClientSessionPacks.
            AssignClientSessionPack.AssignClientSessionPackCommand>, Application.Features.Packs.
                ClientSessionPacks.AssignClientSessionPack.AssignClientSessionPackCommandValidator>();
        services.AddScoped<IValidator<Application.Features.Packs.ClientSessionPacks.
            ListClientSessionPacks.ListClientSessionPacksQuery>, Application.Features.Packs
                .ClientSessionPacks.ListClientSessionPacks.ListClientSessionPacksQueryValidator>();
        services.AddScoped<IValidator<Application.Features.Packs.ClientSessionPacks
            .UpdateClientSessionPackExpectedEndDate.UpdateClientSessionPackExpectedEndDateCommand>,
                Application.Features.Packs.ClientSessionPacks.UpdateClientSessionPackExpectedEndDate
                    .UpdateClientSessionPackExpectedEndDateCommandValidator>();
        services.AddScoped<IValidator<Application.Features.Packs.PackTypes.CreatePackType
            .CreatePackTypeCommand>, Application.Features.Packs.PackTypes.CreatePackType
                .CreatePackTypeCommandValidator>();
        services.AddScoped<IValidator<Application.Features.Packs.PackTypes.ListPackTypes
            .ListPackTypesQuery>, Application.Features.Packs.PackTypes.ListPackTypes.ListPackTypesQueryValidator>();
        services.AddScoped<IValidator<Application.Features.Packs.PackTypes.UpdatePackType
            .UpdatePackTypeCommand>, Application.Features.Packs.PackTypes.UpdatePackType.UpdatePackTypeCommandValidator>();
    }

    private static void AddSessions(IServiceCollection services)
    {
        services.AddScoped<Application.Features.Sessions.CancelSessionByClient.CancelSessionByClientHandler>();
        services.AddScoped<Application.Features.Sessions.CancelSessionByTrainer.CancelSessionByTrainerHandler>();
        services.AddScoped<Application.Features.Sessions.ChangeSessionPack.ChangeSessionPackHandler>();
        services.AddScoped<Application.Features.Sessions.CompleteSession.CompleteSessionHandler>();
        services.AddScoped<Application.Features.Sessions.CreateSession.CreateSessionHandler>();
        services.AddScoped<Application.Features.Sessions.GetSession.GetSessionHandler>();
        services.AddScoped<Application.Features.Sessions.ListSessions.ListSessionsHandler>();
        services.AddScoped<Application.Features.Sessions.MarkSessionNoShow.MarkSessionNoShowHandler>();
        services.AddScoped<Application.Features.Sessions.RescheduleSession.RescheduleSessionHandler>();
        services.AddScoped<Application.Features.Sessions.RestoreSession.RestoreSessionHandler>();

        // Validators
        services.AddScoped<IValidator<Application.Features.Sessions.ChangeSessionPack.
            ChangeSessionPackCommand>, Application.Features.Sessions.ChangeSessionPack
                .ChangeSessionPackCommandValidator>();
        services.AddScoped<IValidator<Application.Features.Sessions.CreateSession
            .CreateSessionCommand>, Application.Features.Sessions
                .CreateSession.CreateSessionCommandValidator>();
        services.AddScoped<IValidator<Application.Features.Sessions.ListSessions
            .ListSessionsQuery>, Application.Features.Sessions.ListSessions.ListSessionsQueryValidator>();
        services.AddScoped<IValidator<Application.Features.Sessions.RescheduleSession
            .RescheduleSessionCommand>, Application.Features.Sessions.RescheduleSession.RescheduleSessionCommandValidator>();
    }

    private static void AddSupplements(IServiceCollection services)
    {
        services.AddScoped<Application.Features.Supplements.ArchiveGlobalSupplement.ArchiveGlobalSupplementHandler>();
        services.AddScoped<Application.Features.Supplements.ArchiveSupplement.ArchiveSupplementHandler>();
        services.AddScoped<Application.Features.Supplements.AssignSupplement.AssignSupplementHandler>();
        services.AddScoped<Application.Features.Supplements.CreateGlobalSupplement.CreateGlobalSupplementHandler>();
        services.AddScoped<Application.Features.Supplements.CreateSupplement.CreateSupplementHandler>();
        services.AddScoped<Application.Features.Supplements.DeactivateSupplementAssignment.DeactivateSupplementAssignmentHandler>();
        services.AddScoped<Application.Features.Supplements.DeleteGlobalSupplement.DeleteGlobalSupplementHandler>();
        services.AddScoped<Application.Features.Supplements.GetGlobalSupplement.GetGlobalSupplementHandler>();
        services.AddScoped<Application.Features.Supplements.GetMySupplementAssignment.GetMySupplementAssignmentHandler>();
        services.AddScoped<Application.Features.Supplements.GetSupplement.GetSupplementHandler>();
        services.AddScoped<Application.Features.Supplements.GetSupplementAssignment.GetSupplementAssignmentHandler>();
        services.AddScoped<Application.Features.Supplements.ListGlobalSupplements.ListGlobalSupplementsHandler>();
        services.AddScoped<Application.Features.Supplements.ListMySupplementAssignments.ListMySupplementAssignmentsHandler>();
        services.AddScoped<Application.Features.Supplements.ListSupplementAssignments.ListSupplementAssignmentsHandler>();
        services.AddScoped<Application.Features.Supplements.ListSupplements.ListSupplementsHandler>();
        services.AddScoped<Application.Features.Supplements.ReactivateGlobalSupplement.ReactivateGlobalSupplementHandler>();
        services.AddScoped<Application.Features.Supplements.ReactivateSupplement.ReactivateSupplementHandler>();
        services.AddScoped<Application.Features.Supplements.ReactivateSupplementAssignment.ReactivateSupplementAssignmentHandler>();
        services.AddScoped<Application.Features.Supplements.UpdateGlobalSupplement.UpdateGlobalSupplementHandler>();
        services.AddScoped<Application.Features.Supplements.UpdateSupplement.UpdateSupplementHandler>();
        services.AddScoped<Application.Features.Supplements.UpdateSupplementAssignment.UpdateSupplementAssignmentHandler>();

        // Validators
        services.AddScoped<IValidator<Application.Features.Supplements
            .AssignSupplement.AssignSupplementCommand>, Application.Features.Supplements
                .AssignSupplement.AssignSupplementCommandValidator>();
        services.AddScoped<IValidator<Application.Features.Supplements
            .CreateGlobalSupplement.CreateGlobalSupplementCommand>, Application.Features.Supplements
            .CreateGlobalSupplement.CreateGlobalSupplementCommandValidator>();
        services.AddScoped<IValidator<Application.Features.Supplements
            .CreateSupplement.CreateSupplementCommand>, Application.Features.Supplements
            .CreateSupplement.CreateSupplementCommandValidator>();
        services.AddScoped<IValidator<Application.Features.Supplements
            .ListGlobalSupplements.ListGlobalSupplementsQuery>, Application.Features.Supplements
            .ListGlobalSupplements.ListGlobalSupplementsQueryValidator>();
        services.AddScoped<IValidator<Application.Features.Supplements
            .ListMySupplementAssignments.ListMySupplementAssignmentsQuery>, Application.Features.Supplements
            .ListMySupplementAssignments.ListMySupplementAssignmentsQueryValidator>();
        services.AddScoped<IValidator<Application.Features.Supplements
            .ListSupplementAssignments.ListSupplementAssignmentsQuery>, Application.Features.Supplements
            .ListSupplementAssignments.ListSupplementAssignmentsQueryValidator>();
        services.AddScoped<IValidator<Application.Features.Supplements
            .ListSupplements.ListSupplementsQuery>, Application.Features.Supplements
            .ListSupplements.ListSupplementsQueryValidator>();
        services.AddScoped<IValidator<Application.Features.Supplements
            .UpdateGlobalSupplement.UpdateGlobalSupplementCommand>, Application.Features.Supplements
            .UpdateGlobalSupplement.UpdateGlobalSupplementCommandValidator>();
        services.AddScoped<IValidator<Application.Features.Supplements
            .UpdateSupplement.UpdateSupplementCommand>, Application.Features.Supplements
            .UpdateSupplement.UpdateSupplementCommandValidator>();
        services.AddScoped<IValidator<Application.Features.Supplements
            .UpdateSupplementAssignment.UpdateSupplementAssignmentCommand>, Application.Features.Supplements
            .UpdateSupplementAssignment.UpdateSupplementAssignmentCommandValidator>();
    }

    private static void AddTrainerSettings(IServiceCollection services)
    {
        services.AddScoped<Application.Features.TrainerSettings.ChangeTimezone.ChangeTimezoneHandler>();
        services.AddScoped<Application.Features.TrainerSettings.GetTrainerSettings.GetTrainerSettingsHandler>();
        services.AddScoped<Application.Features.TrainerSettings.RemoveLogo.RemoveLogoHandler>();
        services.AddScoped<Application.Features.TrainerSettings.ResetBrandingColors.ResetBrandingColorsHandler>();
        services.AddScoped<Application.Features.TrainerSettings.UpdateBranding.UpdateBrandingHandler>();
        services.AddScoped<Application.Features.TrainerSettings.UpdateContacts.UpdateContactsHandler>();

        // Validators
        services.AddScoped<IValidator<Application.Features.TrainerSettings.ChangeTimezone.ChangeTimezoneCommand>,
            Application.Features.TrainerSettings.ChangeTimezone.ChangeTimezoneCommandValidator>();
        services.AddScoped<IValidator<Application.Features.TrainerSettings.ReplaceLogo.ReplaceLogoCommand>,
            Application.Features.TrainerSettings.ReplaceLogo.ReplaceLogoCommandValidator>();
        services.AddScoped<IValidator<Application.Features.TrainerSettings.UpdateBranding.UpdateBrandingCommand>,
            Application.Features.TrainerSettings.UpdateBranding.UpdateBrandingCommandValidator>();
        services.AddScoped<IValidator<Application.Features.TrainerSettings.UpdateContacts.UpdateContactsCommand>,
            Application.Features.TrainerSettings.UpdateContacts.UpdateContactsCommandValidator>();
    }

    private static void AddTraining(IServiceCollection services)
    {
        services.AddScoped<Application.Features.Training.Exercises.ArchiveExercise.ArchiveExerciseHandler>();
        services.AddScoped<Application.Features.Training.Exercises.ArchiveGlobalExercise.ArchiveGlobalExerciseHandler>();
        services.AddScoped<Application.Features.Training.Exercises.CreateExercise.CreateExerciseHandler>();
        services.AddScoped<Application.Features.Training.Exercises.CreateGlobalExercise.CreateGlobalExerciseHandler>();
        services.AddScoped<Application.Features.Training.Exercises.DeleteGlobalExercise.DeleteGlobalExerciseHandler>();
        services.AddScoped<Application.Features.Training.Exercises.GetExercise.GetExerciseHandler>();
        services.AddScoped<Application.Features.Training.Exercises.GetGlobalExercise.GetGlobalExerciseHandler>();
        services.AddScoped<Application.Features.Training.Exercises.ListExercises.ListExercisesHandler>();
        services.AddScoped<Application.Features.Training.Exercises.ListGlobalExercises.ListGlobalExercisesHandler>();
        services.AddScoped<Application.Features.Training.Exercises.ReactivateExercise.ReactivateExerciseHandler>();
        services.AddScoped<Application.Features.Training.Exercises.ReactivateGlobalExercise.ReactivateGlobalExerciseHandler>();
        services.AddScoped<Application.Features.Training.Exercises.UpdateExercise.UpdateExerciseHandler>();
        services.AddScoped<Application.Features.Training.Exercises.UpdateGlobalExercise.UpdateGlobalExerciseHandler>();
        services.AddScoped<Application.Features.Training.ExerciseSetLogs.CorrectExerciseSetLog.CorrectExerciseSetLogHandler>();
        services.AddScoped<Application.Features.Training.ExerciseSetLogs.ListExerciseSetLogs.ListExerciseSetLogsHandler>();
        services.AddScoped<Application.Features.Training.ExerciseSetLogs.RecordExerciseSetLog.RecordExerciseSetLogHandler>();
        services.AddScoped<Application.Features.Training.TrainingPlans.ArchiveTrainingPlan.ArchiveTrainingPlanHandler>();
        services.AddScoped<Application.Features.Training.TrainingPlans.CreateTrainingPlan.CreateTrainingPlanHandler>();
        services.AddScoped<Application.Features.Training.TrainingPlans.GetTrainingPlan.GetTrainingPlanHandler>();
        services.AddScoped<Application.Features.Training.TrainingPlans.ListTrainingPlans.ListTrainingPlansHandler>();
        services.AddScoped<Application.Features.Training.TrainingPlans.ReplaceTrainingPlan.ReplaceTrainingPlanHandler>();
        services.AddScoped<Application.Features.Training.TrainingPlans
            .UpdateTrainingPlanMetadata.UpdateTrainingPlanMetadataHandler>();
        services.AddScoped<Application.Features.Training.TrainingPlans
            .UpdateTrainingPlanStructure.UpdateTrainingPlanStructureHandler>();

        // Validators
        services.AddScoped<IValidator<Application.Features.Training.Exercises.CreateExercise
            .CreateExerciseCommand>, Application.Features.Training.Exercises.CreateExercise
                .CreateExerciseCommandValidator>();
        services.AddScoped<IValidator<Application.Features.Training.Exercises.CreateGlobalExercise
            .CreateGlobalExerciseCommand>, Application.Features.Training.Exercises
                .CreateGlobalExercise.CreateGlobalExerciseCommandValidator>();
        services.AddScoped<IValidator<Application.Features.Training.Exercises.ListExercises
            .ListExercisesQuery>, Application.Features.Training.Exercises.ListExercises.ListExercisesQueryValidator>();
        services.AddScoped<IValidator<Application.Features.Training.Exercises.ListGlobalExercises
            .ListGlobalExercisesQuery>, Application.Features.Training.Exercises.ListGlobalExercises.ListGlobalExercisesQueryValidator>();
        services.AddScoped<IValidator<Application.Features.Training.Exercises.UpdateExercise
            .UpdateExerciseCommand>, Application.Features.Training.Exercises.UpdateExercise.UpdateExerciseCommandValidator>();
        services.AddScoped<IValidator<Application.Features.Training.Exercises.UpdateGlobalExercise
            .UpdateGlobalExerciseCommand>, Application.Features.Training.Exercises.UpdateGlobalExercise.UpdateGlobalExerciseCommandValidator>();
        services.AddScoped<IValidator<Application.Features.Training.ExerciseSetLogs.CorrectExerciseSetLog
            .CorrectExerciseSetLogCommand>, Application.Features.Training.ExerciseSetLogs.CorrectExerciseSetLog.CorrectExerciseSetLogCommandValidator>();
        services.AddScoped<IValidator<Application.Features.Training.ExerciseSetLogs.ListExerciseSetLogs
            .ListExerciseSetLogsQuery>, Application.Features.Training.ExerciseSetLogs.ListExerciseSetLogs.ListExerciseSetLogsQueryValidator>();
        services.AddScoped<IValidator<Application.Features.Training.ExerciseSetLogs.RecordExerciseSetLog
            .RecordExerciseSetLogCommand>, Application.Features.Training.ExerciseSetLogs.RecordExerciseSetLog.RecordExerciseSetLogCommandValidator>();
        services.AddScoped<IValidator<Application.Features.Training.TrainingPlans.CreateTrainingPlan
            .CreateTrainingPlanCommand>, Application.Features.Training.TrainingPlans.CreateTrainingPlan
            .CreateTrainingPlanCommandValidator>();
        services.AddScoped<IValidator<Application.Features.Training.TrainingPlans.ListTrainingPlans
            .ListTrainingPlansQuery>, Application.Features.Training.TrainingPlans.ListTrainingPlans.ListTrainingPlansQueryValidator>();
        services.AddScoped<IValidator<Application.Features.Training.TrainingPlans.ReplaceTrainingPlan
            .ReplaceTrainingPlanCommand>, Application.Features.Training.TrainingPlans.ReplaceTrainingPlan.ReplaceTrainingPlanCommandValidator>();
        services.AddScoped<IValidator<Application.Features.Training.TrainingPlans.UpdateTrainingPlanMetadata
            .UpdateTrainingPlanMetadataCommand>, Application.Features.Training.TrainingPlans.UpdateTrainingPlanMetadata.UpdateTrainingPlanMetadataCommandValidator>();
        services.AddScoped<IValidator<Application.Features.Training.TrainingPlans.UpdateTrainingPlanStructure
            .UpdateTrainingPlanStructureCommand>, Application.Features.Training.TrainingPlans.UpdateTrainingPlanStructure.UpdateTrainingPlanStructureCommandValidator>();
    }
}
