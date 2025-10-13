using System.Text.Json.Serialization;

namespace BennerKurierWorker.Domain.DTOs;

/// <summary>
/// DTO para resposta da API da Kurier contendo distribuições
/// </summary>
public class KurierDistribuicaoResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public List<KurierDistribuicaoDto> Data { get; set; } = new List<KurierDistribuicaoDto>();

    [JsonPropertyName("total")]
    public int Total { get; set; }
}

/// <summary>
/// DTO para uma distribuição individual da API da Kurier
/// </summary>
public class KurierDistribuicaoDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("numeroProcesso")]
    public string NumeroProcesso { get; set; } = string.Empty;

    [JsonPropertyName("numeroDocumento")]
    public string NumeroDocumento { get; set; } = string.Empty;

    [JsonPropertyName("tipoDistribuicao")]
    public string TipoDistribuicao { get; set; } = string.Empty;

    [JsonPropertyName("destinatario")]
    public string Destinatario { get; set; } = string.Empty;

    [JsonPropertyName("dataDistribuicao")]
    public DateTime DataDistribuicao { get; set; }

    [JsonPropertyName("dataLimite")]
    public DateTime? DataLimite { get; set; }

    [JsonPropertyName("conteudo")]
    public string Conteudo { get; set; } = string.Empty;

    [JsonPropertyName("tribunal")]
    public string Tribunal { get; set; } = string.Empty;

    [JsonPropertyName("vara")]
    public string Vara { get; set; } = string.Empty;

    [JsonPropertyName("observacoes")]
    public string? Observacoes { get; set; }
}

/// <summary>
/// DTO para confirmação de recebimento de distribuições
/// </summary>
public class KurierConfirmacaoRequest
{
    [JsonPropertyName("ids")]
    public List<string> Ids { get; set; } = new List<string>();

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.Now;
}

/// <summary>
/// DTO para resposta da confirmação
/// </summary>
public class KurierConfirmacaoResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("confirmedIds")]
    public List<string> ConfirmedIds { get; set; } = new List<string>();

    [JsonPropertyName("failedIds")]
    public List<string> FailedIds { get; set; } = new List<string>();
}