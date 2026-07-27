using System;

namespace Domain.Exceptions;

/// <summary>
/// Violação por invariante que nunca pode ser aceite pela entidade.
/// A Application valida inputs esperados antes de invocar a entidade e devolve
/// Result/Result. O middleware global trata apenas exceções inesperadas.
/// </summary>
public class DomainException : Exception
{
    /// <summary>Cria a exceção com uma mensagem aprensentável ao utilizador.</summary>
    public DomainException(string message) : base(message) { }
}
