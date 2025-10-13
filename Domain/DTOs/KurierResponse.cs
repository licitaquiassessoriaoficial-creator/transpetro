namespace BennerKurierWorker.Domain.DTOs;

/// <summary>
/// Resposta padrão da API Kurier
/// </summary>
/// <typeparam name="T">Tipo dos dados retornados</typeparam>
public class KurierResponse<T>
{
    /// <summary>
    /// Indica se a operação foi bem-sucedida
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Dados retornados pela API
    /// </summary>
    public T Data { get; set; } = default!;

    /// <summary>
    /// Mensagem de resultado ou erro
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Código de status HTTP (opcional)
    /// </summary>
    public int? StatusCode { get; set; }

    /// <summary>
    /// Timestamp da resposta
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}