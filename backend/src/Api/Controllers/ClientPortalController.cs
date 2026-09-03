using Api.Authorization;
using Api.Configuration;
using Api.Contracts.Assessments;
using Api.Contracts.Common;
using Api.Contracts.Portal;
using Application.Features.Assessments.CheckIns.GetMyDueCheckIn;
using Application.Features.Assessments.CheckIns.SubmitCheckInResponse;
using Application.Features.ClientPortal.GetMyNutritionPlan;
using Application.Features.ClientPortal.GetMyProfile;
using Application.Features.ClientPortal.GetMyTrainingPlan;
using Application.Features.ClientPortal.UpdateMyProfile;
using Application.Features.Clients.GetClientBranding;
using Application.Features.Supplements.GetMySupplementAssignment;
using Application.Features.Supplements.ListMySupplementAssignments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
        var page = pageParameters.EffectivePageNumber;
        var size = pageParameters.EffectivePageSize;

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
}
