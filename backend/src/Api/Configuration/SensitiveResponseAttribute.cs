namespace Api.Configuration;

/// <summary>Marca respostas que não podem ser guardadas por browser ou intermediários.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class SensitiveResponseAttribute : Attribute;
