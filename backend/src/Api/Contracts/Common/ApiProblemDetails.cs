using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Contracts.Common;

/// <summary>Forma pública do corpo Problem Details produzido pela API.</summary>
public sealed class ApiProblemDetails : ProblemDetails
{
    /// <summary>Identificador que liga a resposta aos logs do pedido.</summary>
    [JsonPropertyName("correlation_id")]
    public string CorrelationId { get; init; } = string.Empty;

    /// <summary>Erros por campo, presentes apenas em falhas de validação.</summary>
    [JsonPropertyName("errors")]
    public IReadOnlyList<ApiValidationError>? Errors { get; init; }
}

/// <summary>Erro estável associado a um campo do pedido.</summary>
public sealed class ApiValidationError
{
    /// <summary>Campo recebido pela API.</summary>
    [JsonPropertyName("field")]
    public string Field { get; init; } = string.Empty;

    /// <summary>Código estável traduzível pelo frontend.</summary>
    [JsonPropertyName("code")]
    public string Code { get; init; } = string.Empty;

    /// <summary>Descrição usada apenas como fallback controlado.</summary>
    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;
}
