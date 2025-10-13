using Npgsql;
using BennerKurierWorker.Domain;
using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace BennerKurierWorker.Infrastructure;

/// <summary>
/// Gateway para operações de persistência no PostgreSQL (Benner)
/// </summary>
public class BennerPostgreSqlGateway : IBennerGateway
{
    private readonly ILogger<BennerPostgreSqlGateway> _logger;
    private readonly string _connectionString;

    public BennerPostgreSqlGateway(
        ILogger<BennerPostgreSqlGateway> logger,
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
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            
            using var transaction = await connection.BeginTransactionAsync(cancellationToken);

            const string sql = @"
                INSERT INTO distribuicoes (
                    id, numero_processo, numero_documento, tipo_distribuicao, 
                    destinatario, data_distribuicao, data_limite, conteudo, 
                    tribunal, vara, status, data_recebimento, confirmada, 
                    data_confirmacao, observacoes
                ) VALUES (
                    @Id, @NumeroProcesso, @NumeroDocumento, @TipoDistribuicao, 
                    @Destinatario, @DataDistribuicao, @DataLimite, @Conteudo, 
                    @Tribunal, @Vara, @Status, @DataRecebimento, @Confirmada, 
                    @DataConfirmacao, @Observacoes
                )
                ON CONFLICT (id) DO UPDATE SET
                    numero_processo = EXCLUDED.numero_processo,
                    numero_documento = EXCLUDED.numero_documento,
                    tipo_distribuicao = EXCLUDED.tipo_distribuicao,
                    destinatario = EXCLUDED.destinatario,
                    data_distribuicao = EXCLUDED.data_distribuicao,
                    data_limite = EXCLUDED.data_limite,
                    conteudo = EXCLUDED.conteudo,
                    tribunal = EXCLUDED.tribunal,
                    vara = EXCLUDED.vara,
                    status = EXCLUDED.status,
                    observacoes = EXCLUDED.observacoes
                WHERE distribuicoes.confirmada = false";

            var rowsAffected = 0;
            foreach (var distribuicao in distribuicoes)
            {
                distribuicao.DataRecebimento = DateTime.UtcNow;
                distribuicao.Confirmada = false;
                
                rowsAffected += await connection.ExecuteAsync(sql, distribuicao, transaction);
            }

            await transaction.CommitAsync(cancellationToken);
            
            _logger.LogInformation("Salvaram-se {Count} distribuições no banco de dados", rowsAffected);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao salvar distribuições no banco de dados");
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
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            
            using var transaction = await connection.BeginTransactionAsync(cancellationToken);

            const string sql = @"
                INSERT INTO publicacoes (
                    id, numero_processo, tipo_publicacao, titulo, conteudo, 
                    data_publicacao, fonte_publicacao, tribunal, vara, magistrado, 
                    partes, advogados, url_documento, categoria, status, 
                    data_recebimento, confirmada, data_confirmacao, observacoes
                ) VALUES (
                    @Id, @NumeroProcesso, @TipoPublicacao, @Titulo, @Conteudo, 
                    @DataPublicacao, @FontePublicacao, @Tribunal, @Vara, @Magistrado, 
                    @Partes, @Advogados, @UrlDocumento, @Categoria, @Status, 
                    @DataRecebimento, @Confirmada, @DataConfirmacao, @Observacoes
                )
                ON CONFLICT (id) DO UPDATE SET
                    numero_processo = EXCLUDED.numero_processo,
                    tipo_publicacao = EXCLUDED.tipo_publicacao,
                    titulo = EXCLUDED.titulo,
                    conteudo = EXCLUDED.conteudo,
                    data_publicacao = EXCLUDED.data_publicacao,
                    fonte_publicacao = EXCLUDED.fonte_publicacao,
                    tribunal = EXCLUDED.tribunal,
                    vara = EXCLUDED.vara,
                    magistrado = EXCLUDED.magistrado,
                    partes = EXCLUDED.partes,
                    advogados = EXCLUDED.advogados,
                    url_documento = EXCLUDED.url_documento,
                    categoria = EXCLUDED.categoria,
                    status = EXCLUDED.status,
                    observacoes = EXCLUDED.observacoes
                WHERE publicacoes.confirmada = false";

            var rowsAffected = 0;
            foreach (var publicacao in publicacoes)
            {
                publicacao.DataRecebimento = DateTime.UtcNow;
                publicacao.Confirmada = false;
                
                rowsAffected += await connection.ExecuteAsync(sql, publicacao, transaction);
            }

            await transaction.CommitAsync(cancellationToken);
            
            _logger.LogInformation("Salvaram-se {Count} publicações no banco de dados", rowsAffected);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao salvar publicações no banco de dados");
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
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = @"
                SELECT * 
                FROM distribuicoes 
                WHERE confirmada = false 
                ORDER BY data_recebimento
                LIMIT @Limite";

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
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = @"
                SELECT * 
                FROM publicacoes 
                WHERE confirmada = false 
                ORDER BY data_recebimento
                LIMIT @Limite";

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
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = @"
                UPDATE distribuicoes 
                SET confirmada = true, data_confirmacao = CURRENT_TIMESTAMP 
                WHERE id = ANY(@Ids)";

            var rowsAffected = await connection.ExecuteAsync(sql, new { Ids = ids.ToArray() });
            
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
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = @"
                UPDATE publicacoes 
                SET confirmada = true, data_confirmacao = CURRENT_TIMESTAMP 
                WHERE id = ANY(@Ids)";

            var rowsAffected = await connection.ExecuteAsync(sql, new { Ids = ids.ToArray() });
            
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
            using var connection = new NpgsqlConnection(_connectionString);
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
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = @"
                INSERT INTO relatorios_monitoramento (
                    id, data_execucao, quantidade_distribuicoes, quantidade_publicacoes, 
                    amostra_distribuicoes, amostra_publicacoes, status, mensagem, 
                    tempo_execucao_segundos, ultima_atualizacao_distribuicoes, ultima_atualizacao_publicacoes,
                    created_at
                ) VALUES (
                    @Id, @DataExecucao, @QuantidadeDistribuicoes, @QuantidadePublicacoes, 
                    @AmostraDistribuicoes::jsonb, @AmostraPublicacoes::jsonb, @StatusExecucao, @MensagemErro, 
                    @TempoExecucaoMs / 1000.0, @UltimaAtualizacaoDistribuicoes, @UltimaAtualizacaoPublicacoes,
                    CURRENT_TIMESTAMP
                )";

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
