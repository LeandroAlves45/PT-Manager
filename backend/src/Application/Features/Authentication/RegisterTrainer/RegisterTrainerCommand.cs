namespace Application.Features.Authentication.RegisterTrainer;

/// <summary>Solicita o registo públic de uma conta para um personal trainer.</summary>
public sealed record RegisterTrainerCommand(
    string Email,
    string Password,
    string FullName
);
