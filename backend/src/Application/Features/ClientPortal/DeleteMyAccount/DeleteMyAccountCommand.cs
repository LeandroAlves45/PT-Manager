namespace Application.Features.ClientPortal.DeleteMyAccount;

/// <summary>
/// Pedido do cliente para eliminar a própria conta.
/// </summary>
/// <param name="Password">
/// Password atual. Obrigatória quando a conta tem password; uma conta só com Google não tem.
/// </param>
/// <param name="Confirmation">A palavra <see cref="DeleteMyAccountCommandValidator.ConfirmationWord"/>, escrita pelo cliente.</param>
public sealed record DeleteMyAccountCommand(string? Password, string Confirmation);
