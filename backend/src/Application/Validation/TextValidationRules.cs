using FluentValidation;

namespace Application.Validation;

/// <summary>
/// Regras de texto partilhadas pelos validators que recebem texto livre de um cliente.
/// </summary>
internal static class TextValidationRules
{
    /// <summary>Código devolvido quando o texto traz um carácter que não pode ser guardado.</summary>
    internal const string InvalidCharacterCode = "text_invalid_character";

    /// <summary>
    /// Recusa o carácter nulo (U+0000). O PostgreSQL não o guarda em <c>text</c> nem em
    /// <c>jsonb</c>: sem esta regra, a escrita rebentava com um 500 em vez de um 400.
    /// </summary>
    internal static IRuleBuilderOptions<T, string?> WithoutNullCharacter<T>(
        this IRuleBuilder<T, string?> rule) =>
        rule.Must(value => value is null || !value.Contains('\0'))
            .WithErrorCode(InvalidCharacterCode)
            .WithMessage("Text must not contain the null character.");
}
