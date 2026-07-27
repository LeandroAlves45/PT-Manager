using Domain.Exceptions;

namespace Domain.ValueObjects;

/// <summary>
/// Endereço de email validado e normalizado.
/// </summary>
public record EmailAddress
{
    public string Value { get; }
    public string Normalized { get; }

    /// <summary>Cria um endereço de email validado e normalizado.</summary>
    public EmailAddress(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("Email cannot be empty.");

        var trimmed = value.Trim();

        if (!IsValidEmail(trimmed))
            throw new DomainException("Email with the invalid format.");

        Value = trimmed;
        Normalized = trimmed.ToUpperInvariant();
    }

    private static bool IsValidEmail(string email)
    {
        // Valida o comprimento do email, máximo 255 caracteres
        if (email.Length > 255)
            return false;

        // Sem espaços em branco
        if (email.Any(char.IsWhiteSpace))
            return false;

        // Se contem '@', deve ter pelo menos um caractere antes e depois
        var parts = email.Split('@');
        if (parts.Length != 2)
            return false;

        var firstPart = parts[0];
        var secondPart = parts[1];

        if (string.IsNullOrWhiteSpace(firstPart) || string.IsNullOrWhiteSpace(secondPart))
            return false;

        // Garante que o domínio (segunda parte) tem pelo menos um ponto
        if (!secondPart.Contains('.'))
            return false;

        // Garante que o domínio não começa ou termina com um ponto
        if (secondPart.StartsWith('.') || secondPart.EndsWith('.'))
            return false;

        return true;
    }
}
