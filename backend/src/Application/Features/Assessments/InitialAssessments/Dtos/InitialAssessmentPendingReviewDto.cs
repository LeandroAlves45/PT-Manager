namespace Application.Features.Assessments.InitialAssessments.Dtos;

/// <summary>Avaliação inicial gravada pelo cliente que o personal trainer ainda não reviu.</summary>
/// <param name="AssessmentId">Avaliação a abrir.</param>
/// <param name="ClientId">Cliente a quem pertence.</param>
/// <param name="ClientName">Nome do cliente, para a lista não pedir a ficha linha a linha.</param>
/// <param name="SubmittedAt">Instante UTC em que o cliente a gravou.</param>
public sealed record InitialAssessmentPendingReviewDto(
    Guid AssessmentId,
    Guid ClientId,
    string ClientName,
    DateTime SubmittedAt);
