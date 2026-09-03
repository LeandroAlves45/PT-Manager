using Api.Authorization;
using Api.Configuration;
using Api.Contracts.Assessments;
using Application.Features.Assessments.InitialAssessments.CreateInitialAssessment;
using Application.Features.Assessments.InitialAssessments.GetInitialAssessment;
using Application.Features.Assessments.InitialAssessments.UpdateInitialAssessment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>Expõe a avaliação inicial dos clientes do personal trainer autenticado.</summary>
[Route("api/v1")]
[Authorize(ApiPolicyNames.Trainer)]
[SensitiveResponse]
public sealed class InitialAssessmentsController : ApiControllerBase
{
    /// <summary>Cria a avaliação inicial de um cliente.</summary>
    [HttpPost("initial-assessments")]
    public Task<IActionResult> CreateAsync(
        [FromBody] CreateInitialAssessmentRequest request,
        [FromServices] CreateInitialAssessmentHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return RespondCreatedAsync(
            handler.HandleAsync(
                new CreateInitialAssessmentCommand(
                    request.ClientId,
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
                cancellationToken),
            InitialAssessmentResponse.From,
            assessment => $"/api/v1/initial-assessments/{assessment.Id}");
    }

    /// <summary>Devolve a avaliação inicial de um cliente, ou 404 se ainda não existir.</summary>
    [HttpGet("clients/{clientId:guid}/initial-assessment")]
    public Task<IActionResult> GetAsync(
        Guid clientId,
        [FromServices] GetInitialAssessmentHandler handler,
        CancellationToken cancellationToken) =>
        RespondOptionalAsync(
            handler.HandleAsync(
                new GetInitialAssessmentQuery(
                    clientId),
                    cancellationToken),
            InitialAssessmentResponse.From);

    /// <summary>Substitui os campos editáveis de uma avaliação inicial.</summary>
    [HttpPut("initial-assessments/{assessmentId:guid}")]
    public Task<IActionResult> UpdateAsync(
        Guid assessmentId,
        [FromBody] UpdateInitialAssessmentRequest request,
        [FromServices] UpdateInitialAssessmentHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return RespondAsync(
            handler.HandleAsync(
                new UpdateInitialAssessmentCommand(
                    assessmentId,
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
                cancellationToken),
            InitialAssessmentResponse.From);
    }
}
