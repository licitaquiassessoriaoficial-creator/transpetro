namespace BennerKurierWorker.Domain;

/// <summary>
/// Modelo para relatório de monitoramento diário
/// </summary>
public class RelatorioMonitoramento
{
    /// <summary>
    /// Identificador único do relatório
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Data e hora da execução do monitoramento
    /// </summary>
    public DateTime DataExecucao { get; set; } = DateTime.Now;

    /// <summary>
    /// Quantidade de distribuições disponíveis na Kurier
    /// </summary>
    public int QuantidadeDistribuicoes { get; set; }

    /// <summary>
    /// Quantidade de publicações disponíveis na Kurier
    /// </summary>
    public int QuantidadePublicacoes { get; set; }

    /// <summary>
    /// Amostra de distribuições (até 10 registros)
    /// </summary>
    public string AmostraDistribuicoes { get; set; } = string.Empty;

    /// <summary>
    /// Amostra de publicações (até 10 registros)
    /// </summary>
    public string AmostraPublicacoes { get; set; } = string.Empty;

    /// <summary>
    /// Status da execução (Sucesso, Erro, Parcial)
    /// </summary>
    public string Status { get; set; } = "Sucesso";

    /// <summary>
    /// Mensagem de erro ou observações
    /// </summary>
    public string? Mensagem { get; set; }

    /// <summary>
    /// Tempo de execução total em segundos
    /// </summary>
    public double TempoExecucaoSegundos { get; set; }

    /// <summary>
    /// Data da última atualização das distribuições na Kurier
    /// </summary>
    public DateTime? UltimaAtualizacaoDistribuicoes { get; set; }

    /// <summary>
    /// Data da última atualização das publicações na Kurier
    /// </summary>
    public DateTime? UltimaAtualizacaoPublicacoes { get; set; }
}