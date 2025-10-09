using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BennerKurierWorker.Domain;

/// <summary>
/// Entidade para monitoramento diário da API Kurier (Railway mode)
/// Armazena contagens e amostras sem baixar inteiro teor
/// </summary>
[Table("MonitoramentoKurier")]
public class MonitoramentoKurier
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Data e hora da execução do monitoramento
    /// </summary>
    [Required]
    public DateTime DataExecucao { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Quantidade de distribuições disponíveis na API
    /// </summary>
    [Required]
    public int QuantidadeDistribuicoes { get; set; }

    /// <summary>
    /// Quantidade de publicações disponíveis na API
    /// </summary>
    [Required]
    public int QuantidadePublicacoes { get; set; }

    /// <summary>
    /// Amostra das distribuições em formato JSON (até 10 registros)
    /// </summary>
    [Column(TypeName = "jsonb")]
    public string? AmostraDistribuicoes { get; set; }

    /// <summary>
    /// Amostra das publicações em formato JSON (até 10 registros)
    /// </summary>
    [Column(TypeName = "jsonb")]
    public string? AmostraPublicacoes { get; set; }

    /// <summary>
    /// Tempo total de execução em milissegundos
    /// </summary>
    [Required]
    public int TempoExecucaoMs { get; set; }

    /// <summary>
    /// Status da execução: Sucesso, Erro, Parcial
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string StatusExecucao { get; set; } = "Sucesso";

    /// <summary>
    /// Mensagem de erro caso a execução falhe
    /// </summary>
    public string? MensagemErro { get; set; }

    /// <summary>
    /// Modo de execução: RUN_ONCE, CONTINUOUS, SCHEDULED
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string ModoExecucao { get; set; } = "RUN_ONCE";

    /// <summary>
    /// Se TRUE, apenas monitora sem baixar inteiro teor
    /// </summary>
    [Required]
    public bool SomenteMonitoramento { get; set; } = true;

    /// <summary>
    /// Data de criação do registro
    /// </summary>
    [Required]
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Data da última atualização
    /// </summary>
    [Required]
    public DateTime AtualizadoEm { get; set; } = DateTime.UtcNow;
}