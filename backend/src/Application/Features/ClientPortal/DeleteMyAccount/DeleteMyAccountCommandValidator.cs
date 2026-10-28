using Application.Validation;
using FluentValidation;

namespace Application.Features.ClientPortal.DeleteMyAccount;

/// <summary>
/// Exige a palavra de confirmação escrita à mão: um clique por engano não chega para
/// eliminar a conta.
/// </summary>
public sealed class DeleteMyAccountCommandValidator : AbstractValidator<DeleteMyAccountCommand>
{
    /// <summary>Palavra que o cliente escreve para confirmar.</summary>
    public const string ConfirmationWord = "ELIMINAR";

    public DeleteMyAccountCommandValidator()
    {
        RuleFor(command => command.Confirmation)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithErrorCode("portal_account_deletion_confirmation_required")
            .WithMessage($"Type {ConfirmationWord} to confirm.")
            .Must(value => string.Equals(value.Trim(), ConfirmationWord, StringComparison.OrdinalIgnoreCase))
            .WithErrorCode("portal_account_deletion_confirmation_required")
            .WithMessage($"Type {ConfirmationWord} to confirm.");

        RuleFor(command => command.Password)
            .MaximumLength(256)
            .WithErrorCode("portal_account_deletion_password_invalid")
            .WithoutNullCharacter()
            .When(command => command.Password is not null);
    }
}
