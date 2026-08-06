namespace Application.Errors;

/// <summary>
/// Representa um erro de validação associada a um campo do contrato da Application.
/// </summary>
/// <param name="Field">Nome estável do campo no contrato canónico.</param>
/// <param name="Code">Código estável e adequado a tratamento programático.</param>
/// <param name="Message">Descrição segura que pode ser apresentada ao utilizador.</param>
public sealed record ValidationError(string Field, string Code, string Message);
