using Microsoft.Data.SqlClient;
using BennerKurierWorker.Domain;
using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BennerKurierWorker.Infrastructure;

/// <summary>
/// Gateway para operações de persistência no SQL Server do sistema Benner
/// </summary>
public class BennerSqlGateway : IBennerGateway
{
    private readonly ILogger<BennerSqlGateway> _logger;
    private readonly string _connectionString;

    public BennerSqlGateway(
        ILogger<BennerSqlGateway> logger,
        IOptions<BennerSettings> settings)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _connectionString = settings?.Value?.ConnectionString ?? 
            throw new ArgumentNullException(nameof(settings));
    }

    /// <summary>
    /// Salva uma lista de distribuições no banco de dados
    /// </summary>
    public async Task<int> SalvarDistribuicoesAsync(
        IEnumerable<Distribuicao> distribuicoes, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = @"
                MERGE Distribuicoes AS target
                USING (SELECT @Id, @NumeroProcesso, @NumeroDocumento, @TipoDistribuicao, 
                              @Destinatario, @DataDistribuicao, @DataLimite, @Conteudo, 
                              @Tribunal, @Vara, @Status, @DataRecebimento, @Confirmada, 
                              @DataConfirmacao, @Observacoes) AS source 
                       (Id, NumeroProcesso, NumeroDocumento, TipoDistribuicao, 
                        Destinatario, DataDistribuicao, DataLimite, Conteudo, 
                        Tribunal, Vara, Status, DataRecebimento, Confirmada, 
                        DataConfirmacao, Observacoes)
                ON target.Id = source.Id
                WHEN NOT MATCHED THEN
                    INSERT (Id, NumeroProcesso, NumeroDocumento, TipoDistribuicao, 
                           Destinatario, DataDistribuicao, DataLimite, Conteudo, 
                           Tribunal, Vara, Status, DataRecebimento, Confirmada, 
                           DataConfirmacao, Observacoes)
                    VALUES (source.Id, source.NumeroProcesso, source.NumeroDocumento, 
                           source.TipoDistribuicao, source.Destinatario, source.DataDistribuicao, 
                           source.DataLimite, source.Conteudo, source.Tribunal, source.Vara, 
                           source.Status, source.DataRecebimento, source.Confirmada, 
                           source.DataConfirmacao, source.Observacoes)
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
                        Observacoes = source.Observacoes;";

            var rowsAffected = 0;
            foreach (var distribuicao in distribuicoes)
            {
                rowsAffected += await connection.ExecuteAsync(sql, distribuicao);
            }

            _logger.LogInformation("Salvaram-se {Count} distribuições no banco de dados", rowsAffected);
            return rowsAffected;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao salvar distribuições no banco de dados");
            throw;
        }
    }

    /// <summary>
    /// Salva uma lista de publicações no banco de dados
    /// </summary>
    public async Task<int> SalvarPublicacoesAsync(
        IEnumerable<Publicacao> publicacoes, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = @"
                MERGE Publicacoes AS target
                USING (SELECT @Id, @NumeroProcesso, @TipoPublicacao, @Titulo, @Conteudo, 
                              @DataPublicacao, @FontePublicacao, @Tribunal, @Vara, @Magistrado, 
                              @Partes, @Advogados, @UrlDocumento, @Categoria, @Status, 
                              @DataRecebimento, @Confirmada, @DataConfirmacao, @Observacoes) AS source 
                       (Id, NumeroProcesso, TipoPublicacao, Titulo, Conteudo, 
                        DataPublicacao, FontePublicacao, Tribunal, Vara, Magistrado, 
                        Partes, Advogados, UrlDocumento, Categoria, Status, 
                        DataRecebimento, Confirmada, DataConfirmacao, Observacoes)
                ON target.Id = source.Id
                WHEN NOT MATCHED THEN
                    INSERT (Id, NumeroProcesso, TipoPublicacao, Titulo, Conteudo, 
                           DataPublicacao, FontePublicacao, Tribunal, Vara, Magistrado, 
                           Partes, Advogados, UrlDocumento, Categoria, Status, 
                           DataRecebimento, Confirmada, DataConfirmacao, Observacoes)
                    VALUES (source.Id, source.NumeroProcesso, source.TipoPublicacao, 
                           source.Titulo, source.Conteudo, source.DataPublicacao, 
                           source.FontePublicacao, source.Tribunal, source.Vara, 
                           source.Magistrado, source.Partes, source.Advogados, 
                           source.UrlDocumento, source.Categoria, source.Status, 
                           source.DataRecebimento, source.Confirmada, source.DataConfirmacao, 
                           source.Observacoes)
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
                        Observacoes = source.Observacoes;";

            var rowsAffected = 0;
            foreach (var publicacao in publicacoes)
            {
                rowsAffected += await connection.ExecuteAsync(sql, publicacao);
            }

            _logger.LogInformation("Salvaram-se {Count} publicações no banco de dados", rowsAffected);
            return rowsAffected;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao salvar publicações no banco de dados");
            throw;
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
                SELECT TOP (@Limite) * 
                FROM Distribuicoes 
                WHERE Confirmada = 0 
                ORDER BY DataRecebimento";

            var distribuicoes = await connection.QueryAsync<Distribuicao>(sql, new { Limite = limite });
            
            _logger.LogDebug("Obtidas {Count} distribuições não confirmadas", distribuicoes.Count());
            return distribuicoes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter distribuições não confirmadas");
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
                SELECT TOP (@Limite) * 
                FROM Publicacoes 
                WHERE Confirmada = 0 
                ORDER BY DataRecebimento";

            var publicacoes = await connection.QueryAsync<Publicacao>(sql, new { Limite = limite });
            
            _logger.LogDebug("Obtidas {Count} publicações não confirmadas", publicacoes.Count());
            return publicacoes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter publicações não confirmadas");
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
                UPDATE Distribuicoes 
                SET Confirmada = 1, DataConfirmacao = GETDATE() 
                WHERE Id IN @Ids";

            var rowsAffected = await connection.ExecuteAsync(sql, new { Ids = ids });
            
            _logger.LogInformation("Marcadas {Count} distribuições como confirmadas", rowsAffected);
            return rowsAffected;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao marcar distribuições como confirmadas");
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
                UPDATE Publicacoes 
                SET Confirmada = 1, DataConfirmacao = GETDATE() 
                WHERE Id IN @Ids";

            var rowsAffected = await connection.ExecuteAsync(sql, new { Ids = ids });
            
            _logger.LogInformation("Marcadas {Count} publicações como confirmadas", rowsAffected);
            return rowsAffected;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao marcar publicações como confirmadas");
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
            
            _logger.LogDebug("Teste de conexão com banco de dados bem-sucedido");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha no teste de conexão com banco de dados");
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
                INSERT INTO RelatoriosMonitoramento 
                (Id, DataExecucao, QuantidadeDistribuicoes, QuantidadePublicacoes, 
                 AmostraDistribuicoes, AmostraPublicacoes, Status, Mensagem, 
                 TempoExecucaoSegundos, UltimaAtualizacaoDistribuicoes, UltimaAtualizacaoPublicacoes)
                VALUES 
                (@Id, @DataExecucao, @QuantidadeDistribuicoes, @QuantidadePublicacoes, 
                 @AmostraDistribuicoes, @AmostraPublicacoes, @Status, @Mensagem, 
                 @TempoExecucaoSegundos, @UltimaAtualizacaoDistribuicoes, @UltimaAtualizacaoPublicacoes)";

            var rowsAffected = await connection.ExecuteAsync(sql, relatorio);
            
            _logger.LogInformation("Relatório de monitoramento salvo com ID: {Id}", relatorio.Id);
            return rowsAffected > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao salvar relatório de monitoramento");
            return false;
        }
    }
}

/// <summary>
/// Configurações para o gateway Benner
/// </summary>
public class BennerSettings
{
    public string ConnectionString { get; set; } = string.Empty;
}