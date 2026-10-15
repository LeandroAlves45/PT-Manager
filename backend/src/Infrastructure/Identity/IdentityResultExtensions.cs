using Microsoft.AspNetCore.Identity;

namespace Infrastructure.Identity;

internal static class IdentityResultExtensions
{
    /// <summary>
    /// Indica que a criação falhou apenas porque o email já existe. O Identity valida a
    /// password antes do duplicado, por isso uma password fraca nunca chega a parecer um
    /// duplicado: a resposta a uma password fraca é igual para contas novas e existentes.
    /// </summary>
    internal static bool IsOnlyDuplicateAccount(this IdentityResult result) =>
        result.Errors.Any() &&
        result.Errors.All(error =>
            error.Code is nameof(IdentityErrorDescriber.DuplicateEmail)
                or nameof(IdentityErrorDescriber.DuplicateUserName));
}
