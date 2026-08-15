using FluentValidation;

namespace Application.Features.Packs.ClientSessionPacks.UpdateClientSessionPackExpectedEndDate;

/// <summary>Valida o identificador da correção de data esperada.</summary>
public sealed class UpdateClientSessionPackExpectedEndDateCommandValidator
    : AbstractValidator<UpdateClientSessionPackExpectedEndDateCommand>
{
    public UpdateClientSessionPackExpectedEndDateCommandValidator()
    {
        RuleFor(command => command.ClientSessionPackId)
            .NotEmpty()
            .WithErrorCode("client_session_pack_id_required");
    }
}
