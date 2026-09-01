namespace Api.Contracts.Administration;

/// <summary>Motivo estruturado para bloquear conteúdo privado.</summary>
public sealed record BlockContentRequest(string ReasonCode);
