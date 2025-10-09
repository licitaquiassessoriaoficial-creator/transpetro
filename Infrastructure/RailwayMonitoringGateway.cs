using Npgsql;
using BennerKurierWorker.Domain;
using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace BennerKurierWorker.Infrastructure;

/// <summary>
/// Gateway específico para monitoramento na Railway PostgreSQL
/// </summary>
public class RailwayMonitoringGateway : IRailwayMonitoringGateway
{
    private readonly ILogger<RailwayMonitoringGateway> _logger;
    private readonly string _connectionString;

    public RailwayMonitoringGateway(
        ILogger<RailwayMonitoringGateway> logger,
        IOptions<BennerSettings> settings)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _connectionString = settings?.Value?.ConnectionString ?? 
            throw new ArgumentNullException(nameof(settings));
    }

    /// <summary>
    /// Salva resultado do monitoramento Kurier na tabela PostgreSQL
    /// </summary>
    public async Task<bool> SalvarMonitoramentoAsync(
        MonitoramentoKurier monitoramento, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = @"
                INSERT INTO monitoramento_kurier (
                    data_execucao,
                    quantidade_distribuicoes,
                    quantidade_publicacoes,
                    amostra_distribuicoes,
                    amostra_publicacoes,
                    status_execucao,
                    tempo_execucao_ms,
                    observacoes,
                    created_at
                ) VALUES (
                    @DataExecucao,
                    @QuantidadeDistribuicoes,
                    @QuantidadePublicacoes,
                    @AmostraDistribuicoes::jsonb,
                    @AmostraPublicacoes::jsonb,
                    @StatusExecucao,
                    @TempoExecucaoMs,
                    @Observacoes,
                    CURRENT_TIMESTAMP
                )";

            var parameters = new
            {
                DataExecucao = monitoramento.DataExecucao,
                QuantidadeDistribuicoes = monitoramento.QuantidadeDistribuicoes,
                QuantidadePublicacoes = monitoramento.QuantidadePublicacoes,
                AmostraDistribuicoes = JsonSerializer.Serialize(monitoramento.AmostraDistribuicoes),
                AmostraPublicacoes = JsonSerializer.Serialize(monitoramento.AmostraPublicacoes),
                StatusExecucao = monitoramento.StatusExecucao,
                TempoExecucaoMs = monitoramento.TempoExecucaoMs,
                Observacoes = monitoramento.MensagemErro
            };

            var rowsAffected = await connection.ExecuteAsync(sql, parameters, commandTimeout: 30);
            
            _logger.LogInformation(
                "Monitoramento salvo com sucesso: {DataExecucao}, Distribuições: {QtdDist}, Publicações: {QtdPub}",
                monitoramento.DataExecucao,
                monitoramento.QuantidadeDistribuicoes,
                monitoramento.QuantidadePublicacoes);

            return rowsAffected > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao salvar monitoramento no PostgreSQL");
            return false;
        }
    }

    /// <summary>
    /// Busca últimos monitoramentos realizados
    /// </summary>
    public async Task<IEnumerable<MonitoramentoKurier>> BuscarUltimosMonitoramentosAsync(
        int quantidade = 10,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = @"
                SELECT 
                    id,
                    data_execucao as DataExecucao,
                    quantidade_distribuicoes as QuantidadeDistribuicoes,
                    quantidade_publicacoes as QuantidadePublicacoes,
                    amostra_distribuicoes as AmostraDistribuicoes,
                    amostra_publicacoes as AmostraPublicacoes,
                    status_execucao as StatusExecucao,
                    tempo_execucao_ms as TempoExecucaoMs,
                    observacoes as Observacoes,
                    created_at as CreatedAt
                FROM monitoramento_kurier 
                ORDER BY created_at DESC 
                LIMIT @Quantidade";

            var results = await connection.QueryAsync<dynamic>(sql, new { Quantidade = quantidade });
            
            return results.Select(r => new MonitoramentoKurier
            {
                Id = r.id,
                DataExecucao = r.dataexecucao,
                QuantidadeDistribuicoes = r.quantidadedistribuicoes,
                QuantidadePublicacoes = r.quantidadepublicacoes,
                AmostraDistribuicoes = JsonSerializer.Deserialize<List<object>>(r.amostradistribuicoes?.ToString() ?? "[]") ?? new List<object>(),
                AmostraPublicacoes = JsonSerializer.Deserialize<List<object>>(r.amostrapublicacoes?.ToString() ?? "[]") ?? new List<object>(),
                StatusExecucao = r.statusexecucao,
                TempoExecucaoMs = r.tempoexecucaoms,
                MensagemErro = r.observacoes,
                CriadoEm = r.createdat
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar monitoramentos do PostgreSQL");
            return Enumerable.Empty<MonitoramentoKurier>();
        }
    }
}

/// <summary>
/// Interface para o gateway de monitoramento Railway
/// </summary>
public interface IRailwayMonitoringGateway
{
    Task<bool> SalvarMonitoramentoAsync(MonitoramentoKurier monitoramento, CancellationToken cancellationToken = default);
    Task<IEnumerable<MonitoramentoKurier>> BuscarUltimosMonitoramentosAsync(int quantidade = 10, CancellationToken cancellationToken = default);
}