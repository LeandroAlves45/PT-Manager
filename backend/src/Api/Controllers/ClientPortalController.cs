using Api.Authorization;
using Api.Configuration;
using Api.Contracts.Assessments;
using Api.Contracts.Common;
using Api.Contracts.Portal;
using Api.Http;
using Api.Security;
using Application.Features.Assessments.CheckIns.GetMyDueCheckIn;
using Application.Features.Assessments.CheckIns.GetMyNextCheckIn;
using Application.Features.ClientPortal.GetMyInitialAssessment;
using Application.Features.ClientPortal.GetMyPortalHome;
using Application.Features.ClientPortal.SubmitMyInitialAssessment;
using Application.Features.ClientPortal.GetMyWorkoutDay;
using Application.Features.ClientPortal.GetMyWorkoutToday;
using Application.Features.ClientPortal.ListMyMealPlans;
using Application.Features.ClientPortal.SelectMyMealPlan;
using Application.Features.ClientPortal.ListMyWorkoutDays;
using Application.Features.ClientPortal.PlanSupplementIntakes;
using Application.Features.Assessments.CheckIns.SubmitCheckInResponse;
using Application.Features.ClientPortal.GetMyNutritionPlan;
using Application.Features.ClientPortal.DeleteMyAccount;
using Application.Features.ClientPortal.GetMyProfile;
using Application.Features.ClientPortal.GetMyTrainingPlan;
using Application.Features.ClientPortal.RemoveMyAvatar;
using Application.Features.ClientPortal.ReplaceMyAvatar;
using Application.Features.ClientPortal.UpdateMyProfile;
using Application.Features.Clients.GetClientBranding;
using Application.Features.Supplements.GetMySupplementAssignment;
using Application.Features.Supplements.ListMySupplementAssignments;
using Application.Features.Supplements.ListMyTodaySupplementIntakes;
using Application.Features.Supplements.MarkMySupplementIntake;
using Application.Features.Supplements.UnmarkMySupplementIntake;
using Application.Features.Training.ExerciseSetLogs.CorrectMyExerciseSetLog;
using Application.Features.Training.ExerciseSetLogs.DeleteMyExerciseSetLog;
using Application.Features.Training.ExerciseSetLogs.RecordMyExerciseSetLog;
using Application.Features.Training.WorkoutCompletions.CompleteMyWorkout;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Api.Controllers;

/// <summary>Expõe o portal do cliente autenticado.</summary>
[Route("api/v1/portal")]
[Authorize(ApiPolicyNames.Client)]
[SensitiveResponse]
public sealed class ClientPortalController : ApiControllerBase
{
    /// <summary>Devolve a identidade visual do personal trainer.</summary>
    [HttpGet("branding")]
    [ProducesResponseType<PortalBrandingResponse>(StatusCodes.Status200OK)]
    public Task<IActionResult> GetBrandingAsync(
        [FromServices] GetClientBrandingHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(cancellationToken),
            PortalBrandingResponse.From);

    /// <summary>Devolve o resumo dos cartões da home do portal num só pedido.</summary>
    [HttpGet("home")]
    [ProducesResponseType<MyPortalHomeResponse>(StatusCodes.Status200OK)]
    public Task<IActionResult> GetHomeAsync(
        [FromServices] GetMyPortalHomeHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(new GetMyPortalHomeQuery(), cancellationToken),
            MyPortalHomeResponse.From);

    /// <summary>
    /// Diz se o cliente ainda tem de preencher a avaliação inicial. Nunca devolve os dados:
    /// depois de gravada, só o personal trainer a vê.
    /// </summary>
    [HttpGet("my-initial-assessment")]
    [ProducesResponseType<MyInitialAssessmentStatusResponse>(StatusCodes.Status200OK)]
    public Task<IActionResult> GetMyInitialAssessmentStatusAsync(
        [FromServices] GetMyInitialAssessmentStatusHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(new GetMyInitialAssessmentStatusQuery(), cancellationToken),
            MyInitialAssessmentStatusResponse.From);

    /// <summary>
    /// Grava a avaliação inicial preenchida pelo cliente. 409
    /// <c>initial_assessment_already_exists</c> se já houver uma, dele ou do personal trainer.
    /// </summary>
    [HttpPost("my-initial-assessment")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<IActionResult> SubmitMyInitialAssessmentAsync(
        [FromBody] SubmitMyInitialAssessmentRequest request,
        [FromServices] SubmitMyInitialAssessmentHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return RespondAsync(
            handler.HandleAsync(
                new SubmitMyInitialAssessmentCommand(
                    request.WeightKg,
                    request.HeightCm,
                    request.BodyFatPercentage,
                    request.MedicalConditions,
                    request.FitnessLevel,
                    request.ActivityLevel,
                    request.Goals,
                    request.Profession,
                    request.BodyMeasurements?.ToInput(),
                    request.NutritionIntake?.ToInput()),
                cancellationToken));
    }

    /// <summary>Devolve o treino de hoje do cliente, com o que já registou.</summary>
    [HttpGet("my-workout/today")]
    [ProducesResponseType<MyWorkoutTodayResponse>(StatusCodes.Status200OK)]
    public Task<IActionResult> GetMyWorkoutTodayAsync(
        [FromServices] GetMyWorkoutTodayHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(new GetMyWorkoutTodayQuery(), cancellationToken),
            MyWorkoutTodayResponse.From);

    /// <summary>
    /// Lista os dias passados com treino do plano ativo, do início do plano até ontem, no fuso
    /// do personal trainer.
    /// </summary>
    [HttpGet("my-workout/days")]
    [ProducesResponseType<MyPastWorkoutDaysResponse>(StatusCodes.Status200OK)]
    public Task<IActionResult> ListMyWorkoutDaysAsync(
        [FromServices] ListMyWorkoutDaysHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(new ListMyWorkoutDaysQuery(), cancellationToken),
            MyPastWorkoutDaysResponse.From);

    /// <summary>
    /// Devolve um dia passado do plano ativo, só para ler: o planeado, o registado e as notas.
    /// Hoje, dias futuros e dias fora do plano respondem 400.
    /// </summary>
    [HttpGet("my-workout/day")]
    [ProducesResponseType<MyWorkoutTodayResponse>(StatusCodes.Status200OK)]
    public Task<IActionResult> GetMyWorkoutDayAsync(
        [FromQuery] DateOnly? date,
        [FromServices] GetMyWorkoutDayHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(new GetMyWorkoutDayQuery(date), cancellationToken),
            MyWorkoutTodayResponse.From);

    /// <summary>Devolve o plano de treino ativo do cliente.</summary>
    [HttpGet("my-plan")]
    [ProducesResponseType<MyTrainingPlanResponse>(StatusCodes.Status200OK)]
    public Task<IActionResult> GetMyPlanAsync(
        [FromServices] GetMyTrainingPlanHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(cancellationToken),
            MyTrainingPlanResponse.From);

    /// <summary>Lista os planos alimentares ativos do cliente, para escolher qual seguir.</summary>
    [HttpGet("my-meal-plans")]
    [ProducesResponseType<MyMealPlansResponse>(StatusCodes.Status200OK)]
    public Task<IActionResult> ListMyMealPlansAsync(
        [FromServices] ListMyMealPlansHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(handler.HandleAsync(cancellationToken), MyMealPlansResponse.From);

    /// <summary>
    /// Guarda na conta o plano alimentar que o cliente escolheu seguir. 404 se o plano não for
    /// dele ou estiver arquivado.
    /// </summary>
    [HttpPut("my-meal-plans/selection")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<IActionResult> SelectMyMealPlanAsync(
        [FromBody] SelectMyMealPlanRequest request,
        [FromServices] SelectMyMealPlanHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return RespondAsync(
            handler.HandleAsync(new SelectMyMealPlanCommand(request.MealPlanId), cancellationToken));
    }

    /// <summary>
    /// Devolve o plano alimentar que o cliente escolheu. Sem escolha, 404
    /// <c>portal_nutrition_plan_not_selected</c> se houver planos para escolher.
    /// </summary>
    [HttpGet("my-nutrition")]
    [ProducesResponseType<MyNutritionPlanResponse>(StatusCodes.Status200OK)]
    public Task<IActionResult> GetMyNutritionAsync(
        [FromServices] GetMyNutritionPlanHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(cancellationToken),
            MyNutritionPlanResponse.From);

    /// <summary>Devolve o perfil do cliente autenticado.</summary>
    [HttpGet("my-profile")]
    [ProducesResponseType<MyProfileResponse>(StatusCodes.Status200OK)]
    public Task<IActionResult> GetMyProfileAsync(
        [FromServices] GetMyProfileHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(cancellationToken),
            MyProfileResponse.From);

    /// <summary>Atualiza o nome e os contactos do cliente autenticado.</summary>
    [HttpPatch("my-profile")]
    [ProducesResponseType<MyProfileResponse>(StatusCodes.Status200OK)]
    public Task<IActionResult> UpdateMyProfileAsync(
        [FromBody] UpdateMyProfileRequest request,
        [FromServices] UpdateMyProfileHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return RespondAsync(
            handler.HandleAsync(
                new UpdateMyProfileCommand(
                    request.Name,
                    request.Phone,
                    request.EmergencyContactName,
                    request.EmergencyContactPhone),
                cancellationToken),
            MyProfileResponse.From);
    }

    /// <summary>
    /// Substitui a fotografia de perfil do próprio cliente a partir de uma parte
    /// multipart única chamada "file".
    /// </summary>
    [HttpPut("my-profile/avatar")]
    [ProducesResponseType<MyProfileResponse>(StatusCodes.Status200OK)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(FormFileMediaUpload.MaxRequestBytes)]
    [RequestFormLimits(
        MultipartBodyLengthLimit = FormFileMediaUpload.MaxRequestBytes,
        MultipartHeadersLengthLimit = FormFileMediaUpload.MaxPartHeadersBytes,
        ValueCountLimit = FormFileMediaUpload.MaxFormValues)]
    [EnableRateLimiting(ApiRateLimitPolicyNames.MediaUpload)]
    public async Task<IActionResult> ReplaceMyAvatarAsync(
        [FromForm(Name = "file")] IFormFile? file,
        [FromServices] ReplaceMyAvatarHandler handler,
        CancellationToken cancellationToken)
    {
        var upload = FormFileMediaUpload.From(file);
        await using (upload?.Content)
        {
            return await RespondAsync(
                handler.HandleAsync(new ReplaceMyAvatarCommand(upload!), cancellationToken),
                MyProfileResponse.From);
        }
    }

    /// <summary>Remove o avatar personalizado e agenda a eliminação depois do commit.</summary>
    [HttpDelete("my-profile/avatar")]
    [ProducesResponseType<MyProfileResponse>(StatusCodes.Status200OK)]
    public Task<IActionResult> RemoveMyAvatarAsync(
        [FromServices] RemoveMyAvatarHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(cancellationToken),
            MyProfileResponse.From);

    /// <summary>
    /// Elimina a conta do cliente autenticado: acesso e dados que o identificam são apagados,
    /// as sessões terminam em todos os dispositivos e o histórico fica no tenant do personal
    /// trainer sem identificação. Irreversível.
    /// </summary>
    /// <remarks>
    /// Partilha o limite da alteração de password (5 em 15 minutos por conta e IP): os dois
    /// confirmam a password atual e o limite trava a adivinhação.
    /// </remarks>
    [HttpPost("my-account/deletion")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [EnableRateLimiting(ApiRateLimitPolicyNames.ChangePassword)]
    public async Task<IActionResult> DeleteMyAccountAsync(
        [FromBody] DeleteMyAccountRequest request,
        [FromServices] DeleteMyAccountHandler handler,
        [FromServices] ILogger<ClientPortalController> logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = await handler.HandleAsync(
            new DeleteMyAccountCommand(request.Password, request.Confirmation ?? string.Empty),
            cancellationToken);

        // Sem auditoria administrativa (essa é só do superuser): o rasto é este evento,
        // sem dados pessoais. O utilizador fica identificado pelo id no contexto do pedido.
        AuthenticationSecurityLog.Result(logger, SecurityLogEvents.AccountDeletion, "delete_my_account", result);
        return result.IsSuccess ? NoContent() : ApiResultMapper.ToProblem(this, result.Error!);
    }

    /// <summary>Devolve o próximo check-in agendado do cliente, se existir.</summary>
    [HttpGet("my-check-ins/next")]
    [ProducesResponseType<MyNextCheckInResponse>(StatusCodes.Status200OK)]
    public Task<IActionResult> GetMyNextCheckInAsync(
        [FromServices] GetMyNextCheckInHandler handler,
        CancellationToken cancellationToken) =>
        RespondOptionalAsync(
            handler.HandleAsync(new GetMyNextCheckInQuery(), cancellationToken),
            MyNextCheckInResponse.From);

    /// <summary>Devolve o check-in pendente de resposta, se existir.</summary>
    [HttpGet("my-check-ins/due")]
    [ProducesResponseType<MyCheckInResponse>(StatusCodes.Status200OK)]
    public Task<IActionResult> GetMyDueCheckInAsync(
        [FromServices] GetMyDueCheckInHandler handler,
        CancellationToken cancellationToken) =>
        RespondOptionalAsync(
            handler.HandleAsync(new GetMyDueCheckInQuery(), cancellationToken),
            MyCheckInResponse.From);

    /// <summary>Submete a resposta do cliente a um check-in agendado.</summary>
    [HttpPost("check-ins/{checkInId:guid}/respond")]
    [ProducesResponseType<MyCheckInResponse>(StatusCodes.Status200OK)]
    public Task<IActionResult> SubmitCheckInAnswerAsync(
        Guid checkInId,
        [FromBody] CheckInAnswerRequest request,
        [FromServices] SubmitCheckInResponseHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return RespondAsync(
            handler.HandleAsync(
                new SubmitCheckInResponseCommand(
                    checkInId,
                    request.WeightKg,
                    request.BodyFatPercentage,
                    request.Notes,
                    request.BodyMeasurements?.ToInput(),
                    request.Feedback?.ToInput(),
                    request.TrainingAdherenceScore,
                    request.NutritionAdherenceScore),
                cancellationToken),
            MyCheckInResponse.From);
    }

    /// <summary>Lista uma página dos suplementos atribuídos ao cliente.</summary>
    [HttpGet("my-supplements")]
    [ProducesResponseType<PagedResponse<MySupplementAssignmentResponse>>(StatusCodes.Status200OK)]
    public Task<IActionResult> ListMySupplementsAsync(
        [FromQuery] PageParameters pageParameters,
        [FromServices] ListMySupplementAssignmentsHandler handler,
        CancellationToken cancellationToken)
    {
        var page = pageParameters.GetEffectivePageNumber();
        var size = pageParameters.GetEffectivePageSize();

        return RespondAsync(
            handler.HandleAsync(
                new ListMySupplementAssignmentsQuery(page, size),
                cancellationToken),
            result => PagedResponse<MySupplementAssignmentResponse>.From(
                result, page, size, MySupplementAssignmentResponse.From));
    }

    /// <summary>Devolve um suplemento atribuído ao próprio cliente.</summary>
    [HttpGet("my-supplements/{assignmentId:guid}")]
    [ProducesResponseType<MySupplementAssignmentResponse>(StatusCodes.Status200OK)]
    public Task<IActionResult> GetMySupplementAsync(
        Guid assignmentId,
        [FromServices] GetMySupplementAssignmentHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(
                new GetMySupplementAssignmentQuery(assignmentId), cancellationToken),
            MySupplementAssignmentResponse.From);

    /// <summary>Regista uma série de hoje no plano ativo do cliente.</summary>
    [HttpPost("exercise-set-logs")]
    [ProducesResponseType<MyExerciseSetLogResponse>(StatusCodes.Status200OK)]
    public Task<IActionResult> RecordMyExerciseSetLogAsync(
        [FromBody] RecordMyExerciseSetLogRequest request,
        [FromServices] RecordMyExerciseSetLogHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return RespondAsync(
            handler.HandleAsync(
                new RecordMyExerciseSetLogCommand(
                    request.TrainingPlanDayExerciseId,
                    request.SetNumber,
                    request.WeightKg,
                    request.RepsDone,
                    request.Rpe,
                    request.Notes),
                cancellationToken),
            MyExerciseSetLogResponse.From);
    }

    /// <summary>Corrige uma série registada hoje.</summary>
    [HttpPatch("exercise-set-logs/{exerciseSetLogId:guid}")]
    [ProducesResponseType<MyExerciseSetLogResponse>(StatusCodes.Status200OK)]
    public Task<IActionResult> CorrectMyExerciseSetLogAsync(
        Guid exerciseSetLogId,
        [FromBody] CorrectMyExerciseSetLogRequest request,
        [FromServices] CorrectMyExerciseSetLogHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return RespondAsync(
            handler.HandleAsync(
                new CorrectMyExerciseSetLogCommand(
                    exerciseSetLogId,
                    request.WeightKg,
                    request.RepsDone,
                    request.Rpe,
                    request.Notes),
                cancellationToken),
            MyExerciseSetLogResponse.From);
    }

    /// <summary>Desmarca uma série própria de hoje, antes de concluir o treino do dia.</summary>
    [HttpDelete("exercise-set-logs/{exerciseSetLogId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<IActionResult> DeleteMyExerciseSetLogAsync(
        Guid exerciseSetLogId,
        [FromServices] DeleteMyExerciseSetLogHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(new DeleteMyExerciseSetLogCommand(
                exerciseSetLogId), cancellationToken));

    /// <summary>Conclui o treino de hoje para um dia do plano; repetir devolve a mesma conclusão.</summary>
    [HttpPost("workout-completions")]
    [ProducesResponseType<MyWorkoutCompletionResponse>(StatusCodes.Status200OK)]
    public Task<IActionResult> CompleteMyWorkoutAsync(
        [FromBody] CompleteMyWorkoutRequest request,
        [FromServices] CompleteMyWorkoutHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return RespondAsync(
            handler.HandleAsync(
                new CompleteMyWorkoutCommand(request.TrainingPlanDayId, request.Notes),
                cancellationToken),
            MyWorkoutCompletionResponse.From);
    }

    /// <summary>
    /// Suplementos do plano alimentar escolhido (refeições e fora delas) com a toma de hoje.
    /// Sem escolha, 404 <c>portal_nutrition_plan_not_selected</c>: antes da primeira escolha
    /// não há tomas do plano.
    /// </summary>
    [HttpGet("my-plan-supplements/today")]
    [ProducesResponseType<MyPlanSupplementsTodayResponse>(StatusCodes.Status200OK)]
    public Task<IActionResult> GetMyPlanSupplementsTodayAsync(
        [FromServices] GetMyPlanSupplementsTodayHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(handler.HandleAsync(cancellationToken), MyPlanSupplementsTodayResponse.From);

    /// <summary>Marca a toma de hoje de um suplemento do plano escolhido; repetir devolve a mesma.</summary>
    [HttpPut("my-plan-supplements/{itemId:guid}/intakes/today")]
    [ProducesResponseType<MyPlanSupplementIntakeResponse>(StatusCodes.Status200OK)]
    public Task<IActionResult> MarkMyPlanSupplementIntakeAsync(
        Guid itemId,
        [FromServices] MarkMyPlanSupplementIntakeHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(new MyPlanSupplementIntakeCommand(itemId), cancellationToken),
            MyPlanSupplementIntakeResponse.From);

    /// <summary>Desmarca a toma de hoje de um suplemento do plano escolhido.</summary>
    [HttpDelete("my-plan-supplements/{itemId:guid}/intakes/today")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<IActionResult> UnmarkMyPlanSupplementIntakeAsync(
        Guid itemId,
        [FromServices] UnmarkMyPlanSupplementIntakeHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(handler.HandleAsync(new MyPlanSupplementIntakeCommand(itemId), cancellationToken));

    /// <summary>Devolve os suplementos ativos com o estado da toma de hoje.</summary>
    [HttpGet("my-supplements/intakes/today")]
    [ProducesResponseType<MyTodaySupplementIntakesResponse>(StatusCodes.Status200OK)]
    public Task<IActionResult> ListMyTodaySupplementIntakesAsync(
        [FromServices] ListMyTodaySupplementIntakesHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(cancellationToken),
            MyTodaySupplementIntakesResponse.From);

    /// <summary>Marca a toma de hoje; repetir devolve a mesma toma.</summary>
    [HttpPut("my-supplements/{assignmentId:guid}/intakes/today")]
    [ProducesResponseType<MySupplementIntakeResponse>(StatusCodes.Status200OK)]
    public Task<IActionResult> MarkMySupplementIntakeAsync(
        Guid assignmentId,
        [FromServices] MarkMySupplementIntakeHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(new MarkMySupplementIntakeCommand(assignmentId), cancellationToken),
            MySupplementIntakeResponse.From);

    /// <summary>Desmarca a toma de hoje; sem toma registada responde 204 na mesma.</summary>
    [HttpDelete("my-supplements/{assignmentId:guid}/intakes/today")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<IActionResult> UnmarkMySupplementIntakeAsync(
        Guid assignmentId,
        [FromServices] UnmarkMySupplementIntakeHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(new UnmarkMySupplementIntakeCommand(assignmentId), cancellationToken));
}
