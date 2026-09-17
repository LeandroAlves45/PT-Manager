using Api.Authorization;
using Api.Configuration;
using Api.Contracts.Assessments;
using Api.Contracts.Common;
using Api.Contracts.Portal;
using Api.Http;
using Application.Features.Assessments.CheckIns.GetMyDueCheckIn;
using Application.Features.Assessments.CheckIns.SubmitCheckInResponse;
using Application.Features.ClientPortal.GetMyNutritionPlan;
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
    public Task<IActionResult> GetBrandingAsync(
        [FromServices] GetClientBrandingHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(cancellationToken),
            PortalBrandingResponse.From);

    /// <summary>Devolve o plano de treino ativo do cliente.</summary>
    [HttpGet("my-plan")]
    public Task<IActionResult> GetMyPlanAsync(
        [FromServices] GetMyTrainingPlanHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(cancellationToken),
            MyTrainingPlanResponse.From);

    /// <summary>Devolve o plano alimentar ativo do cliente.</summary>
    [HttpGet("my-nutrition")]
    public Task<IActionResult> GetMyNutritionAsync(
        [FromServices] GetMyNutritionPlanHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(cancellationToken),
            MyNutritionPlanResponse.From);

    /// <summary>Devolve o perfil do cliente autenticado.</summary>
    [HttpGet("my-profile")]
    public Task<IActionResult> GetMyProfileAsync(
        [FromServices] GetMyProfileHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(cancellationToken),
            MyProfileResponse.From);

    /// <summary>Atualiza os contactos do cliente autenticado.</summary>
    [HttpPatch("my-profile")]
    public Task<IActionResult> UpdateMyProfileAsync(
        [FromBody] UpdateMyProfileRequest request,
        [FromServices] UpdateMyProfileHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return RespondAsync(
            handler.HandleAsync(
                new UpdateMyProfileCommand(
                    request.ContactEmail,
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
    public Task<IActionResult> RemoveMyAvatarAsync(
        [FromServices] RemoveMyAvatarHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(cancellationToken),
            MyProfileResponse.From);

    /// <summary>Devolve o check-in pendente de resposta, se existir.</summary>
    [HttpGet("my-check-ins/due")]
    public Task<IActionResult> GetMyDueCheckInAsync(
        [FromServices] GetMyDueCheckInHandler handler,
        CancellationToken cancellationToken) =>
        RespondOptionalAsync(
            handler.HandleAsync(new GetMyDueCheckInQuery(), cancellationToken),
            MyCheckInResponse.From);

    /// <summary>Submete a resposta do cliente a um check-in agendado.</summary>
    [HttpPost("check-ins/{checkInId:guid}/respond")]
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
    public Task<IActionResult> DeleteMyExerciseSetLogAsync(
        Guid exerciseSetLogId,
        [FromServices] DeleteMyExerciseSetLogHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(new DeleteMyExerciseSetLogCommand(
                exerciseSetLogId), cancellationToken));

    /// <summary>Conclui o treino de hoje para um dia do plano; repetir devolve a mesma conclusão.</summary>
    [HttpPost("workout-completions")]
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

    /// <summary>Devolve os suplementos ativos com o estado da toma de hoje.</summary>
    [HttpGet("my-supplements/intakes/today")]
    public Task<IActionResult> ListMyTodaySupplementIntakesAsync(
        [FromServices] ListMyTodaySupplementIntakesHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(cancellationToken),
            MyTodaySupplementIntakesResponse.From);

    /// <summary>Marca a toma de hoje; repetir devolve a mesma toma.</summary>
    [HttpPut("my-supplements/{assignmentId:guid}/intakes/today")]
    public Task<IActionResult> MarkMySupplementIntakeAsync(
        Guid assignmentId,
        [FromServices] MarkMySupplementIntakeHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(new MarkMySupplementIntakeCommand(assignmentId), cancellationToken),
            MySupplementIntakeResponse.From);

    /// <summary>Desmarca a toma de hoje; sem toma registada responde 204 na mesma.</summary>
    [HttpDelete("my-supplements/{assignmentId:guid}/intakes/today")]
    public Task<IActionResult> UnmarkMySupplementIntakeAsync(
        Guid assignmentId,
        [FromServices] UnmarkMySupplementIntakeHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(new UnmarkMySupplementIntakeCommand(assignmentId), cancellationToken));
}
