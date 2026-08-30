namespace Api.Security;

/// <summary>Marca uma opção que só aceita pedidos com um header Origin da allowlist.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireOriginAttribute : Attribute;
