using System.Text.Json.Serialization;

namespace BennerKurierWorker.Domain.DTOs;

/// <summary>
/// DTO para resposta da API da Kurier contendo publicações
/// </summary>
public class KurierPublicacaoResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public List<KurierPublicacaoDto> Data { get; set; } = new List<KurierPublicacaoDto>();

    [JsonPropertyName("total")]
    public int Total { get; set; }
}

/// <summary>
/// DTO para uma publicação individual da API da Kurier
/// </summary>
public class KurierPublicacaoDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("numeroProcesso")]
    public string NumeroProcesso { get; set; } = string.Empty;

    [JsonPropertyName("tipoPublicacao")]
    public string TipoPublicacao { get; set; } = string.Empty;

    [JsonPropertyName("titulo")]
    public string Titulo { get; set; } = string.Empty;

    [JsonPropertyName("conteudo")]
    public string Conteudo { get; set; } = string.Empty;

    [JsonPropertyName("dataPublicacao")]
    public DateTime DataPublicacao { get; set; }

    [JsonPropertyName("fontePublicacao")]
    public string FontePublicacao { get; set; } = string.Empty;

    [JsonPropertyName("tribunal")]
    public string Tribunal { get; set; } = string.Empty;

    [JsonPropertyName("vara")]
    public string Vara { get; set; } = string.Empty;

    [JsonPropertyName("magistrado")]
    public string? Magistrado { get; set; }

    [JsonPropertyName("partes")]
    public string Partes { get; set; } = string.Empty;

    [JsonPropertyName("advogados")]
    public string? Advogados { get; set; }

    [JsonPropertyName("urlDocumento")]
    public string? UrlDocumento { get; set; }

    [JsonPropertyName("categoria")]
    public string Categoria { get; set; } = string.Empty;

    [JsonPropertyName("observacoes")]
    public string? Observacoes { get; set; }
}