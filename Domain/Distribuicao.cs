namespace BennerKurierWorker.Domain;

/// <summary>
/// Representa uma distribuição recebida da Kurier
/// </summary>
public class Distribuicao
{
    /// <summary>
    /// Identificador único da distribuição na Kurier
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Número do processo relacionado à distribuição
    /// </summary>
    public string NumeroProcesso { get; set; } = string.Empty;

    /// <summary>
    /// Número do documento da distribuição
    /// </summary>
    public string NumeroDocumento { get; set; } = string.Empty;

    /// <summary>
    /// Tipo da distribuição (ex: Citação, Intimação, etc.)
    /// </summary>
    public string TipoDistribuicao { get; set; } = string.Empty;

    /// <summary>
    /// Nome da parte/interessado
    /// </summary>
    public string Destinatario { get; set; } = string.Empty;

    /// <summary>
    /// Data da distribuição
    /// </summary>
    public DateTime DataDistribuicao { get; set; }

    /// <summary>
    /// Data limite para manifestação
    /// </summary>
    public DateTime? DataLimite { get; set; }

    /// <summary>
    /// Conteúdo ou descrição da distribuição
    /// </summary>
    public string Conteudo { get; set; } = string.Empty;

    /// <summary>
    /// Tribunal de origem
    /// </summary>
    public string Tribunal { get; set; } = string.Empty;

    /// <summary>
    /// Vara ou órgão julgador
    /// </summary>
    public string Vara { get; set; } = string.Empty;

    /// <summary>
    /// Status da distribuição (Pendente, Processada, etc.)
    /// </summary>
    public string Status { get; set; } = "Pendente";

    /// <summary>
    /// Data de recebimento no sistema Benner
    /// </summary>
    public DateTime DataRecebimento { get; set; } = DateTime.Now;

    /// <summary>
    /// Indica se a distribuição já foi confirmada para a Kurier
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