using Application.Common.Abstractions;
using FluentValidation;

namespace Application.Features.Clients.UpdateClient;

/// <summary>Valida o identificador e o perfil completo de atualização.</summary>
public sealed class UpdateClientCommandValidator : AbstractValidator<UpdateClientCommand>
{
    /// <summary>Inicializa regras determinísticas usando o relógio injetado.</summary>
    public UpdateClientCommandValidator(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);

        RuleFor(client => client.ClientId)
            .NotEmpty()
            .WithErrorCode("client_id_required");

        RuleFor(client => client.Name)
            .NotEmpty()
            .MaximumLength(255)
            .WithErrorCode("client_name_invalid");

        RuleFor(client => client.ContactEmail)
            .MaximumLength(255)
            .EmailAddress()
            .When(client => !string.IsNullOrWhiteSpace(client.ContactEmail))
            .WithErrorCode("client_email_invalid");

        RuleFor(client => client.Phone)
            .NotEmpty()
            .MaximumLength(32)
            .WithErrorCode("client_phone_invalid");

        RuleFor(client => client.BirthDate)
            .NotEmpty()
            .LessThanOrEqualTo(DateOnly.FromDateTime(clock.UtcNow))
            .WithErrorCode("client_birth_date_invalid");

        RuleFor(client => client.Sex)
            .Must(value => value?.Trim() is "male" or "female")
            .WithErrorCode("client_sex_invalid");

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
