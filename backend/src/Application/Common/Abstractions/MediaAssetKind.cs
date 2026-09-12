namespace Application.Common.Abstractions;

/// <summary>
/// Tipo lógico de asset gerido. Determina a pasta de destino escolhida pelo
/// adapter de storage; a Application nunca constrói caminhos de fornecedores.
/// </summary>
public enum MediaAssetKind
{
    TrainerLogo,
    ClientAvatar
}
