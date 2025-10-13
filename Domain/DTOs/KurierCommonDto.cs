using System.Text.Json.Serialization;

namespace BennerKurierWorker.Domain.DTOs;

/// <summary>
/// DTO genérico para resposta de erro da API da Kurier
/// </summary>
public class KurierErrorResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("errorCode")]
    public string? ErrorCode { get; set; }

    [JsonPropertyName("details")]
    public object? Details { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// DTO para parâmetros de consulta paginada
/// </summary>
public class KurierPaginationRequest
{
    [JsonPropertyName("page")]
    public int Page { get; set; } = 1;

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; } = 100;

    [JsonPropertyName("dataInicio")]
    public DateTime? DataInicio { get; set; }

    [JsonPropertyName("dataFim")]
    public DateTime? DataFim { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }
}