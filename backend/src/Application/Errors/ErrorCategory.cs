namespace Application.Errors;

/// <summary>
/// Classifica uma falha esperada independentemente do transporte utilizado.
/// A fronteira HTTP usa esta categoria para escolher o status code mas a Application
/// não depende de HTTP.
/// </summary>
public enum ErrorCategory
{
    /// <summary>O pedido viola uma ou mais regras de validação.</summary>
    Validation,

    /// <summary>O recurso não existe ou não é visível no tenant efetivo.</summary>
    NotFound,

    /// <summary>O estado atual ou uma constraint impede a operação.</summary>
    Conflict,

    /// <summary>As credenciais ou o token não estabelecem uma identidade válida.</summary>
    Unauthorized,

    /// <summary>A identidade é válida mas não tem permissão para executar a operação.</summary>
    Forbidden,

    /// <summary>A subscrição ou o limite comercial bloqueia a operação.</summary>
    PaymentRequired,

    /// <summary>Uma dependência externa está temporariamente indisponível.</summary>
    ExternalDependency
}
