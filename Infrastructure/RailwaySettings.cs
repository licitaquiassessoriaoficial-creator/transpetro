namespace BennerKurierWorker.Infrastructure;

/// <summary>
/// Configurações específicas do Railway PostgreSQL
/// </summary>
public class RailwaySettings
{
    public string ConnectionString { get; set; } = string.Empty;
    public bool RunOnce { get; set; } = true;
    public string Environment { get; set; } = "production";
    public int MonitoringIntervalMinutes { get; set; } = 60;
}