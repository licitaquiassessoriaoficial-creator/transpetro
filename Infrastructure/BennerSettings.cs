namespace BennerKurierWorker.Infrastructure;

/// <summary>
/// Configurações para conexão com banco de dados Benner
/// </summary>
public class BennerSettings
{
    /// <summary>
    /// String de conexão com o banco de dados
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;
}