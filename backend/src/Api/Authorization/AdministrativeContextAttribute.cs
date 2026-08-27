namespace Api.Authorization;

/// <summary>
/// Marca endpoints que podem estabelecer contexto administrativo após autorização.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AdministrativeContextAttribute : Attribute;
