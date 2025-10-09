namespace BennerKurierWorker.Domain;

/// <summary>
/// Representa uma publicação recebida da Kurier
/// </summary>
public class Publicacao
{
    /// <summary>
    /// Identificador único da publicação na Kurier
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Número do processo relacionado à publicação
    /// </summary>
    public string NumeroProcesso { get; set; } = string.Empty;

    /// <summary>
    /// Tipo da publicação (ex: Sentença, Despacho, Acórdão, etc.)
    /// </summary>
    public string TipoPublicacao { get; set; } = string.Empty;

    /// <summary>
    /// Título ou assunto da publicação
    /// </summary>
    public string Titulo { get; set; } = string.Empty;

    /// <summary>
    /// Conteúdo completo da publicação
    /// </summary>
    public string Conteudo { get; set; } = string.Empty;

    /// <summary>
    /// Data da publicação oficial
    /// </summary>
    public DateTime DataPublicacao { get; set; }

    /// <summary>
    /// Fonte da publicação (Diário Oficial, site do tribunal, etc.)
    /// </summary>
    public string FontePublicacao { get; set; } = string.Empty;

    /// <summary>
    /// Tribunal responsável pela publicação
    /// </summary>
    public string Tribunal { get; set; } = string.Empty;

    /// <summary>
    /// Vara ou órgão julgador
    /// </summary>
    public string Vara { get; set; } = string.Empty;

    /// <summary>
    /// Nome do juiz ou relator
    /// </summary>
    public string? Magistrado { get; set; }

    /// <summary>
    /// Partes envolvidas no processo
    /// </summary>
    public string Partes { get; set; } = string.Empty;

    /// <summary>
    /// Advogados mencionados na publicação
    /// </summary>
    public string? Advogados { get; set; }

    /// <summary>
    /// URL ou link para o documento original, se disponível
    /// </summary>
    public string? UrlDocumento { get; set; }

    /// <summary>
    /// Categoria da publicação (Cível, Criminal, Trabalhista, etc.)
    /// </summary>
    public string Categoria { get; set; } = string.Empty;

    /// <summary>
    /// Status da publicação (Pendente, Processada, etc.)
    /// </summary>
    public string Status { get; set; } = "Pendente";

    /// <summary>
    /// Data de recebimento no sistema Benner
    /// </summary>
    public DateTime DataRecebimento { get; set; } = DateTime.Now;

    /// <summary>
    /// Indica se a publicação já foi confirmada para a Kurier
    /// </summary>
    public bool Confirmada { get; set; } = false;

    /// <summary>
    /// Data da confirmação para a Kurier
    /// </summary>
    public DateTime? DataConfirmacao { get; set; }

    /// <summary>
    /// Observações adicionais
    /// </summary>
    public string? Observacoes { get; set; }
}