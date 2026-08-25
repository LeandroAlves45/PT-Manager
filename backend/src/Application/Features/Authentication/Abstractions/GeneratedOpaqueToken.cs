namespace Application.Features.Authentication.Abstractions;

/// <summary>
/// Transporta um token opaco apenas durante a fronteira de emissão. O valor bruto
/// é entregue ao destinatário e apenas o hash é persistido.
/// </summary>
public sealed record GeneratedOpaqueToken(string RawToken, string TokenHash);
