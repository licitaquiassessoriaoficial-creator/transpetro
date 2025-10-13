using System.Text.Json.Serialization;

namespace BennerKurierWorker.Domain.DTOs;

/// <summary>
/// DTO para resposta de quantidade de distribuições disponíveis
/// </summary>
public class KurierQuantidadeDistribuicoesResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("quantidade")]
    public int Quantidade { get; set; }

    [JsonPropertyName("dataConsulta")]
    public DateTime DataConsulta { get; set; }

    [JsonPropertyName("ultimaAtualizacao")]
    public DateTime? UltimaAtualizacao { get; set; }
}

/// <summary>
/// DTO para resposta de quantidade de publicações disponíveis
/// </summary>
public class KurierQuantidadePublicacoesResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("quantidade")]
    public int Quantidade { get; set; }

    [JsonPropertyName("dataConsulta")]
    public DateTime DataConsulta { get; set; }

    [JsonPropertyName("ultimaAtualizacao")]
    public DateTime? UltimaAtualizacao { get; set; }
}

/// <summary>
/// DTO para resumo de distribuição
/// </summary>
public class KurierDistribuicaoResumoDto
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

    [JsonPropertyName("tribunal")]
    public string Tribunal { get; set; } = string.Empty;

    [JsonPropertyName("vara")]
    public string Vara { get; set; } = string.Empty;
}

/// <summary>
/// DTO para resumo de publicação
/// </summary>
public class KurierPublicacaoResumoDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("numeroProcesso")]
    public string NumeroProcesso { get; set; } = string.Empty;

    [JsonPropertyName("tipoPublicacao")]
    public string TipoPublicacao { get; set; } = string.Empty;

    [JsonPropertyName("titulo")]
    public string Titulo { get; set; } = string.Empty;

    [JsonPropertyName("dataPublicacao")]
    public DateTime DataPublicacao { get; set; }

    [JsonPropertyName("fontePublicacao")]
    public string FontePublicacao { get; set; } = string.Empty;

    [JsonPropertyName("tribunal")]
    public string Tribunal { get; set; } = string.Empty;

    [JsonPropertyName("vara")]
    public string Vara { get; set; } = string.Empty;

    [JsonPropertyName("categoria")]
    public string Categoria { get; set; } = string.Empty;
}

/// <summary>
/// DTO para resposta de resumos de distribuições
/// </summary>
public class KurierDistribuicoesResumoResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public List<KurierDistribuicaoResumoDto> Data { get; set; } = new List<KurierDistribuicaoResumoDto>();

    [JsonPropertyName("total")]
    public int Total { get; set; }
}

/// <summary>
/// DTO para resposta de resumos de publicações
/// </summary>
public class KurierPublicacoesResumoResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public List<KurierPublicacaoResumoDto> Data { get; set; } = new List<KurierPublicacaoResumoDto>();

    [JsonPropertyName("total")]
    public int Total { get; set; }
}