namespace Api.Contracts.Administration;

/// <summary>Motivo estruturado e nota livre opcional para bloquear conteúdo privado.</summary>
public sealed record BlockContentRequest(string ReasonCode, string? Note = null);
