namespace Application.Features.Authentication.Dtos;

/// <summary>Conta do personal trainer e ainda sujeita á confirmação de email.</summary>
public sealed record RegisteredTrainerDto(
    Guid UserId,
    Guid TrainerId,
    string Email,
    DateTime TrialEndsAt
);
