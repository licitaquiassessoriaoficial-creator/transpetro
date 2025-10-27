using System.Data.SqlClient;
using BennerKurierWorker.Domain;
using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace BennerKurierWorker.Infrastructure;

/// <summary>
/// Gateway para operações de persistência no SQL Server (Benner)
/// </summary>
public class BennerSqlServerGateway : IBennerGateway
{
    private readonly ILogger<BennerSqlServerGateway> _logger;
    private readonly string _connectionString;

    public BennerSqlServerGateway(
        ILogger<BennerSqlServerGateway> logger,
        IOptions<BennerSettings> settings)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _connectionString = settings?.Value?.ConnectionString ?? 
            throw new ArgumentNullException(nameof(settings));
    }

    /// <summary>
    /// Salva uma lista de distribuições no banco de dados com transação
    /// </summary>
    public async Task<bool> SalvarDistribuicoesAsync(
        IEnumerable<Distribuicao> distribuicoes, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            
            using var transaction = await connection.BeginTransactionAsync();

            const string sql = @"
                MERGE KURIER_Distribuicoes AS target
                USING (SELECT 
                    @KurierId AS KurierId,
                    @NumeroProcesso AS NumeroProcesso,
                    @NumeroDocumento AS NumeroDocumento,
                    @TipoDistribuicao AS TipoDistribuicao,
                    @Destinatario AS Destinatario,
                    @DataDistribuicao AS DataDistribuicao,
                    @DataLimite AS DataLimite,
                    @Conteudo AS Conteudo,
                    @Tribunal AS Tribunal,
                    @Vara AS Vara,
                    @Status AS Status,
                    @DataRecebimento AS DataRecebimento,
                    @Confirmada AS Confirmada,
                    @DataConfirmacao AS DataConfirmacao,
                    @Observacoes AS Observacoes
                ) AS source ON target.KurierId = source.KurierId
                WHEN MATCHED AND target.Confirmada = 0 THEN
                    UPDATE SET 
                        NumeroProcesso = source.NumeroProcesso,
                        NumeroDocumento = source.NumeroDocumento,
                        TipoDistribuicao = source.TipoDistribuicao,
                        Destinatario = source.Destinatario,
                        DataDistribuicao = source.DataDistribuicao,
                        DataLimite = source.DataLimite,
                        Conteudo = source.Conteudo,
                        Tribunal = source.Tribunal,
                        Vara = source.Vara,
                        Status = source.Status,
                        Observacoes = source.Observacoes,
                        AtualizadoEm = GETUTCDATE()
                WHEN NOT MATCHED THEN
                    INSERT (KurierId, NumeroProcesso, NumeroDocumento, TipoDistribuicao, 
                            Destinatario, DataDistribuicao, DataLimite, Conteudo, 
                            Tribunal, Vara, Status, DataRecebimento, Confirmada, 
                            DataConfirmacao, Observacoes)
                    VALUES (source.KurierId, source.NumeroProcesso, source.NumeroDocumento, 
                            source.TipoDistribuicao, source.Destinatario, source.DataDistribuicao, 
                            source.DataLimite, source.Conteudo, source.Tribunal, source.Vara, 
                            source.Status, source.DataRecebimento, source.Confirmada, 
                            source.DataConfirmacao, source.Observacoes);";

            var rowsAffected = 0;
            foreach (var distribuicao in distribuicoes)
            {
                distribuicao.DataRecebimento = DateTime.UtcNow;
                distribuicao.Confirmada = false;
                
                var parametros = new
                {
                    KurierId = distribuicao.Id,
                    NumeroProcesso = distribuicao.NumeroProcesso,
                    NumeroDocumento = distribuicao.NumeroDocumento,
                    TipoDistribuicao = distribuicao.TipoDistribuicao,
                    Destinatario = distribuicao.Destinatario,
                    DataDistribuicao = distribuicao.DataDistribuicao,
                    DataLimite = distribuicao.DataLimite,
                    Conteudo = distribuicao.Conteudo,
                    Tribunal = distribuicao.Tribunal,
                    Vara = distribuicao.Vara,
                    Status = distribuicao.Status ?? "Pendente",
                    DataRecebimento = distribuicao.DataRecebimento,
                    Confirmada = distribuicao.Confirmada,
                    DataConfirmacao = distribuicao.DataConfirmacao,
                    Observacoes = distribuicao.Observacoes
                };
                
                rowsAffected += await connection.ExecuteAsync(sql, parametros, transaction);
            }

            await transaction.CommitAsync();
            
            _logger.LogInformation("✅ Salvaram-se {Count} distribuições no banco Benner SQL Server", rowsAffected);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro ao salvar distribuições no banco Benner");
            return false;
        }
    }

    /// <summary>
    /// Salva uma lista de publicações no banco de dados com transação
    /// </summary>
    public async Task<bool> SalvarPublicacoesAsync(
        IEnumerable<Publicacao> publicacoes, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            
            using var transaction = await connection.BeginTransactionAsync();

            const string sql = @"
                MERGE KURIER_Publicacoes AS target
                USING (SELECT 
                    @KurierId AS KurierId,
                    @NumeroProcesso AS NumeroProcesso,
                    @TipoPublicacao AS TipoPublicacao,
                    @Titulo AS Titulo,
                    @Conteudo AS Conteudo,
                    @DataPublicacao AS DataPublicacao,
                    @FontePublicacao AS FontePublicacao,
                    @Tribunal AS Tribunal,
                    @Vara AS Vara,
                    @Magistrado AS Magistrado,
                    @Partes AS Partes,
                    @Advogados AS Advogados,
                    @UrlDocumento AS UrlDocumento,
                    @Categoria AS Categoria,
                    @Status AS Status,
                    @DataRecebimento AS DataRecebimento,
                    @Confirmada AS Confirmada,
                    @DataConfirmacao AS DataConfirmacao,
                    @Observacoes AS Observacoes
                ) AS source ON target.KurierId = source.KurierId
                WHEN MATCHED AND target.Confirmada = 0 THEN
                    UPDATE SET 
                        NumeroProcesso = source.NumeroProcesso,
                        TipoPublicacao = source.TipoPublicacao,
                        Titulo = source.Titulo,
                        Conteudo = source.Conteudo,
                        DataPublicacao = source.DataPublicacao,
                        FontePublicacao = source.FontePublicacao,
                        Tribunal = source.Tribunal,
                        Vara = source.Vara,
                        Magistrado = source.Magistrado,
                        Partes = source.Partes,
                        Advogados = source.Advogados,
                        UrlDocumento = source.UrlDocumento,
                        Categoria = source.Categoria,
                        Status = source.Status,
                        Observacoes = source.Observacoes,
                        AtualizadoEm = GETUTCDATE()
                WHEN NOT MATCHED THEN
                    INSERT (KurierId, NumeroProcesso, TipoPublicacao, Titulo, Conteudo, 
                            DataPublicacao, FontePublicacao, Tribunal, Vara, Magistrado, 
                            Partes, Advogados, UrlDocumento, Categoria, Status, 
                            DataRecebimento, Confirmada, DataConfirmacao, Observacoes)
                    VALUES (source.KurierId, source.NumeroProcesso, source.TipoPublicacao, 
                            source.Titulo, source.Conteudo, source.DataPublicacao, 
                            source.FontePublicacao, source.Tribunal, source.Vara, 
                            source.Magistrado, source.Partes, source.Advogados, 
                            source.UrlDocumento, source.Categoria, source.Status, 
                            source.DataRecebimento, source.Confirmada, source.DataConfirmacao, 
                            source.Observacoes);";

            var rowsAffected = 0;
            foreach (var publicacao in publicacoes)
            {
                publicacao.DataRecebimento = DateTime.UtcNow;
                publicacao.Confirmada = false;
                
                var parametros = new
                {
                    KurierId = publicacao.Id,
                    NumeroProcesso = publicacao.NumeroProcesso,
                    TipoPublicacao = publicacao.TipoPublicacao,
                    Titulo = publicacao.Titulo,
                    Conteudo = publicacao.Conteudo,
                    DataPublicacao = publicacao.DataPublicacao,
                    FontePublicacao = publicacao.FontePublicacao,
                    Tribunal = publicacao.Tribunal,
                    Vara = publicacao.Vara,
                    Magistrado = publicacao.Magistrado,
                    Partes = publicacao.Partes,
                    Advogados = publicacao.Advogados,
                    UrlDocumento = publicacao.UrlDocumento,
                    Categoria = publicacao.Categoria,
                    Status = publicacao.Status ?? "Pendente",
                    DataRecebimento = publicacao.DataRecebimento,
                    Confirmada = publicacao.Confirmada,
                    DataConfirmacao = publicacao.DataConfirmacao,
                    Observacoes = publicacao.Observacoes
                };
                
                rowsAffected += await connection.ExecuteAsync(sql, parametros, transaction);
            }

            await transaction.CommitAsync();
            
            _logger.LogInformation("✅ Salvaram-se {Count} publicações no banco Benner SQL Server", rowsAffected);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro ao salvar publicações no banco Benner");
            return false;
        }
    }

    /// <summary>
    /// Obtém distribuições não confirmadas para envio de confirmação
    /// </summary>
    public async Task<IEnumerable<Distribuicao>> ObterDistribuicoesNaoConfirmadasAsync(
        int limite = 100, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = @"
                SELECT TOP (@Limite)
                    KurierId AS Id,
                    NumeroProcesso,
                    NumeroDocumento,
                    TipoDistribuicao,
                    Destinatario,
                    DataDistribuicao,
                    DataLimite,
                    Conteudo,
                    Tribunal,
                    Vara,
                    Status,
                    DataRecebimento,
                    Confirmada,
                    DataConfirmacao,
                    Observacoes
                FROM KURIER_Distribuicoes 
                WHERE Confirmada = 0 
                ORDER BY DataRecebimento";

            var distribuicoes = await connection.QueryAsync<Distribuicao>(sql, new { Limite = limite });
            
            _logger.LogDebug("📦 Obtidas {Count} distribuições não confirmadas", distribuicoes.Count());
            return distribuicoes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro ao obter distribuições não confirmadas");
            throw;
        }
    }

    /// <summary>
    /// Obtém publicações não confirmadas para envio de confirmação
    /// </summary>
    public async Task<IEnumerable<Publicacao>> ObterPublicacoesNaoConfirmadasAsync(
        int limite = 100, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = @"
                SELECT TOP (@Limite)
                    KurierId AS Id,
                    NumeroProcesso,
                    TipoPublicacao,
                    Titulo,
                    Conteudo,
                    DataPublicacao,
                    FontePublicacao,
                    Tribunal,
                    Vara,
                    Magistrado,
                    Partes,
                    Advogados,
                    UrlDocumento,
                    Categoria,
                    Status,
                    DataRecebimento,
                    Confirmada,
                    DataConfirmacao,
                    Observacoes
                FROM KURIER_Publicacoes 
                WHERE Confirmada = 0 
                ORDER BY DataRecebimento";

            var publicacoes = await connection.QueryAsync<Publicacao>(sql, new { Limite = limite });
            
            _logger.LogDebug("📜 Obtidas {Count} publicações não confirmadas", publicacoes.Count());
            return publicacoes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro ao obter publicações não confirmadas");
            throw;
        }
    }

    /// <summary>
    /// Marca distribuições como confirmadas
    /// </summary>
    public async Task<int> MarcarDistribuicoesComoConfirmadasAsync(
        IEnumerable<string> ids, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = @"
                UPDATE KURIER_Distribuicoes 
                SET Confirmada = 1, DataConfirmacao = GETUTCDATE(), AtualizadoEm = GETUTCDATE()
                WHERE KurierId IN @Ids";

            var rowsAffected = await connection.ExecuteAsync(sql, new { Ids = ids.ToArray() });
            
            _logger.LogInformation("✅ Marcadas {Count} distribuições como confirmadas", rowsAffected);
            return rowsAffected;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro ao marcar distribuições como confirmadas");
            throw;
        }
    }

    /// <summary>
    /// Marca publicações como confirmadas
    /// </summary>
    public async Task<int> MarcarPublicacoesComoConfirmadasAsync(
        IEnumerable<string> ids, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = @"
                UPDATE KURIER_Publicacoes 
                SET Confirmada = 1, DataConfirmacao = GETUTCDATE(), AtualizadoEm = GETUTCDATE()
                WHERE KurierId IN @Ids";

            var rowsAffected = await connection.ExecuteAsync(sql, new { Ids = ids.ToArray() });
            
            _logger.LogInformation("✅ Marcadas {Count} publicações como confirmadas", rowsAffected);
            return rowsAffected;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro ao marcar publicações como confirmadas");
            throw;
        }
    }

    /// <summary>
    /// Verifica se a conexão com o banco está funcionando
    /// </summary>
    public async Task<bool> TestarConexaoAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            
            const string sql = "SELECT 1";
            await connection.ExecuteScalarAsync(sql);
            
            _logger.LogInformation("✅ Conexão com banco Benner SQL Server funcionando");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Falha na conexão com banco Benner SQL Server: {Message}", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Salva um relatório de monitoramento no banco de dados
    /// </summary>
    public async Task<bool> SalvarRelatorioMonitoramentoAsync(
        MonitoramentoKurier relatorio, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = @"
                INSERT INTO KURIER_Monitoramento (
                    DataExecucao, QuantidadeDistribuicoes, QuantidadePublicacoes, 
                    AmostraDistribuicoes, AmostraPublicacoes, StatusExecucao, MensagemErro, 
                    TempoExecucaoMs, ModoExecucao, SomenteMonitoramento
                ) VALUES (
                    @DataExecucao, @QuantidadeDistribuicoes, @QuantidadePublicacoes, 
                    @AmostraDistribuicoes, @AmostraPublicacoes, @StatusExecucao, @MensagemErro, 
                    @TempoExecucaoMs, @ModoExecucao, @SomenteMonitoramento
                )";

            var parametros = new
            {
                relatorio.DataExecucao,
                relatorio.QuantidadeDistribuicoes,
                relatorio.QuantidadePublicacoes,
                AmostraDistribuicoes = JsonSerializer.Serialize(relatorio.AmostraDistribuicoes),
                AmostraPublicacoes = JsonSerializer.Serialize(relatorio.AmostraPublicacoes),
                relatorio.StatusExecucao,
                relatorio.MensagemErro,
                relatorio.TempoExecucaoMs,
                relatorio.ModoExecucao,
                relatorio.SomenteMonitoramento
            };

            var rowsAffected = await connection.ExecuteAsync(sql, parametros);
            
            _logger.LogInformation("✅ Relatório de monitoramento salvo no Benner");
            return rowsAffected > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro ao salvar relatório de monitoramento no Benner");
            return false;
        }
    }
}