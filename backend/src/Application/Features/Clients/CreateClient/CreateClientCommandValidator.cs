using Application.Common.Abstractions;
using FluentValidation;

namespace Application.Features.Clients.CreateClient;

/// <summary>Valida o contrato canónico antes de construir value objects.</summary>
public sealed class CreateClientCommandValidator : AbstractValidator<CreateClientCommand>
{
    /// <summary>Inicializa regras determinísticas usando o relógio injetado.</summary>
    public CreateClientCommandValidator(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);

        RuleFor(client => client.Name)
            .NotEmpty()
            .MaximumLength(255)
            .WithErrorCode("client_name_invalid")
            .WithMessage("Client name must contain between 1 and 255 characters.");

        RuleFor(client => client.ContactEmail)
            .MaximumLength(255)
            .EmailAddress()
            .When(client => !string.IsNullOrWhiteSpace(client.ContactEmail))
            .WithErrorCode("client_email_invalid")
            .WithMessage("Client email is invalid.");

        RuleFor(client => client.Phone)
            .NotEmpty()
            .MaximumLength(32)
            .WithErrorCode("client_phone_invalid")
            .WithMessage("Client phone must contain between 1 and 32 characters.");

        RuleFor(client => client.BirthDate)
            .NotEmpty()
            .LessThanOrEqualTo(DateOnly.FromDateTime(clock.UtcNow))
            .WithErrorCode("client_birth_date_invalid")
            .WithMessage("Birth date is required and cannot be in the future.");

        RuleFor(client => client.Sex)
            .Must(value => value?.Trim() is "male" or "female")
            .WithErrorCode("client_sex_invalid")
            .WithMessage("Biological sex must be male or female.");

        RuleFor(client => client.Objective)
            .MaximumLength(255)
            .When(client => !string.IsNullOrWhiteSpace(client.Objective))
            .WithErrorCode("client_objective_too_long");

        RuleFor(client => client.EmergencyContactName)
            .MaximumLength(255)
            .When(client => !string.IsNullOrWhiteSpace(client.EmergencyContactName))
            .WithErrorCode("client_emergency_name_too_long");

        RuleFor(client => client.EmergencyContactPhone)
            .MaximumLength(32)
            .When(client => !string.IsNullOrWhiteSpace(client.EmergencyContactPhone))
            .WithErrorCode("client_emergency_phone_too_long");
    }
}
