using BennerKurierWorker.Domain;
using BennerKurierWorker.Infrastructure;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BennerKurierWorker.Application;

/// <summary>
/// Serviço principal que orquestra a sincronização entre Kurier e Benner
/// Suporta execução contínua ou única (para Railway/Cloud)
/// </summary>
public class KurierJobs : BackgroundService
{
    private readonly ILogger<KurierJobs> _logger;
    private readonly IKurierClient _kurierClient;
    private readonly IBennerGateway? _bennerGateway;
    private readonly IRailwayMonitoringGateway? _railwayGateway;
    private readonly KurierJobsSettings _settings;
    private readonly MonitoringSettings _monitoringSettings;
    private readonly Timer? _timer;
    private readonly bool _runOnce;

    // Construtor para Railway (apenas monitoramento)
    public KurierJobs(
        ILogger<KurierJobs> logger,
        IKurierClient kurierClient,
        IRailwayMonitoringGateway railwayGateway,
        IOptions<KurierJobsSettings> settings,
        IOptions<MonitoringSettings> monitoringSettings)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _kurierClient = kurierClient ?? throw new ArgumentNullException(nameof(kurierClient));
        _railwayGateway = railwayGateway ?? throw new ArgumentNullException(nameof(railwayGateway));
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _monitoringSettings = monitoringSettings?.Value ?? throw new ArgumentNullException(nameof(monitoringSettings));

        _runOnce = true; // Railway sempre executa uma vez
        _logger.LogInformation("KurierJobs configurado para Railway (modo monitoramento)");
    }

    // Construtor para execução local (sincronização completa)
    public KurierJobs(
        ILogger<KurierJobs> logger,
        IKurierClient kurierClient,
        IBennerGateway bennerGateway,
        IOptions<KurierJobsSettings> settings,
        IOptions<MonitoringSettings> monitoringSettings)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _kurierClient = kurierClient ?? throw new ArgumentNullException(nameof(kurierClient));
        _bennerGateway = bennerGateway ?? throw new ArgumentNullException(nameof(bennerGateway));
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _monitoringSettings = monitoringSettings?.Value ?? throw new ArgumentNullException(nameof(monitoringSettings));

        // Verificar se deve executar apenas uma vez
        _runOnce = Environment.GetEnvironmentVariable("RUN_ONCE")?.ToLowerInvariant() == "true";

        if (!_runOnce)
        {
            // Inicializar timer apenas se não for execução única
            _timer = new Timer(ExecuteJobsCallback!, null, Timeout.Infinite, Timeout.Infinite);
        }

        _logger.LogInformation("KurierJobs configurado para execução local. Modo: {Mode}", _runOnce ? "RUN_ONCE" : "CONTINUOUS");
    }

    /// <summary>
    /// Inicia o serviço em background
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Kurier Jobs iniciado. Modo: {Mode}, Intervalo: {IntervalMinutes} minutos", 
            _runOnce ? "RUN_ONCE" : "CONTINUOUS", _settings.IntervalMinutes);

        // Testar conectividades antes de iniciar
        if (!await TestarConectividadeAsync(stoppingToken))
        {
            _logger.LogCritical("Falha nos testes de conectividade. Serviço será encerrado.");
            return;
        }

        if (_runOnce)
        {
            // Modo execução única - executa e encerra
            if (_monitoringSettings.Enabled)
            {
                await RodarMonitoramentoAsync(stoppingToken);
            }
            else
            {
                await ExecuteJobsAsync(stoppingToken);
            }
            
            _logger.LogInformation("Execução única concluída. Encerrando aplicação.");
            return;
        }
        else
        {
            // Modo contínuo - execução periódica
            await ExecuteJobsAsync(stoppingToken);

            // Configurar timer para execuções periódicas
            var intervalMilliseconds = _settings.IntervalMinutes * 60 * 1000;
            _timer?.Change(intervalMilliseconds, intervalMilliseconds);

            // Manter o serviço rodando
            try
            {
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Kurier Jobs foi cancelado");
            }
        }
    }

    /// <summary>
    /// Callback do timer para execução periódica
    /// </summary>
    private void ExecuteJobsCallback(object state)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await ExecuteJobsAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro durante execução periódica dos jobs");
            }
        });
    }

    /// <summary>
    /// Executa todos os jobs de sincronização
    /// </summary>
    private async Task ExecuteJobsAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Iniciando ciclo de sincronização Kurier-Benner");

            // Job 1: Sincronizar Distribuições
            await SincronizarDistribuicoesAsync(cancellationToken);

            // Job 2: Sincronizar Publicações
            await SincronizarPublicacoesAsync(cancellationToken);

            // Job 3: Confirmar Distribuições
            await ConfirmarDistribuicoesAsync(cancellationToken);

            // Job 4: Confirmar Publicações
            await ConfirmarPublicacoesAsync(cancellationToken);

            _logger.LogInformation("Ciclo de sincronização concluído com sucesso");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro durante ciclo de sincronização");
        }
    }

    /// <summary>
    /// Sincroniza distribuições da Kurier para o Benner
    /// </summary>
    private async Task SincronizarDistribuicoesAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Sincronizando distribuições...");

            var distribuicoes = await _kurierClient.ConsultarDistribuicoesAsync(cancellationToken);

            if (distribuicoes.Any())
            {
                var savedCount = await _bennerGateway.SalvarDistribuicoesAsync(distribuicoes, cancellationToken);
                
                _logger.LogInformation("Sincronizadas {Count} distribuições", savedCount);
            }
            else
            {
                _logger.LogInformation("Nenhuma distribuição nova encontrada");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao sincronizar distribuições");
        }
    }

    /// <summary>
    /// Sincroniza publicações da Kurier para o Benner
    /// </summary>
    private async Task SincronizarPublicacoesAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Sincronizando publicações...");

            var publicacoes = await _kurierClient.ConsultarPublicacoesAsync(true, cancellationToken);

            if (publicacoes.Any())
            {
                var savedCount = await _bennerGateway.SalvarPublicacoesAsync(publicacoes, cancellationToken);
                
                _logger.LogInformation("Sincronizadas {Count} publicações", savedCount);
            }
            else
            {
                _logger.LogInformation("Nenhuma publicação nova encontrada");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao sincronizar publicações");
        }
    }

    /// <summary>
    /// Confirma distribuições recebidas de volta para a Kurier
    /// </summary>
    private async Task ConfirmarDistribuicoesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var distribuicoesNaoConfirmadas = await _bennerGateway.ObterDistribuicoesNaoConfirmadasAsync(
                _settings.ConfirmationBatchSize, cancellationToken);

            if (distribuicoesNaoConfirmadas.Any())
            {
                _logger.LogInformation("Confirmando {Count} distribuições", distribuicoesNaoConfirmadas.Count());

                var ids = distribuicoesNaoConfirmadas.Select(d => d.Id).ToList();

                await _kurierClient.ConfirmarDistribuicoesAsync(ids, cancellationToken);

                await _bennerGateway.MarcarDistribuicoesComoConfirmadasAsync(
                    ids, cancellationToken);
                
                _logger.LogInformation("Confirmadas {Count} distribuições com sucesso", ids.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao confirmar distribuições");
        }
    }

    /// <summary>
    /// Confirma publicações recebidas de volta para a Kurier
    /// </summary>
    private async Task ConfirmarPublicacoesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var publicacoesNaoConfirmadas = await _bennerGateway.ObterPublicacoesNaoConfirmadasAsync(
                _settings.ConfirmationBatchSize, cancellationToken);

            if (publicacoesNaoConfirmadas.Any())
            {
                _logger.LogInformation("Confirmando {Count} publicações", publicacoesNaoConfirmadas.Count());

                var ids = publicacoesNaoConfirmadas.Select(p => p.Id).ToList();

                await _kurierClient.ConfirmarPublicacoesAsync(ids, cancellationToken);

                await _bennerGateway.MarcarPublicacoesComoConfirmadasAsync(
                    ids, cancellationToken);
                
                _logger.LogInformation("Confirmadas {Count} publicações com sucesso", ids.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao confirmar publicações");
        }
    }

    /// <summary>
    /// Testa conectividade com Kurier e Benner
    /// </summary>
    private async Task<bool> TestarConectividadeAsync(CancellationToken cancellationToken)
    {
        var success = true;

        // Testar conexão com banco de dados
        if (!await _bennerGateway.TestarConexaoAsync(cancellationToken))
        {
            _logger.LogError("Falha na conexão com banco de dados Benner");
            success = false;
        }

        // Testar conexão com API Kurier (através de uma consulta simples)
        try
        {
            await _kurierClient.ConsultarDistribuicoesAsync(cancellationToken);
            _logger.LogInformation("Conexão com API Kurier testada com sucesso");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha na conexão com API Kurier");
            success = false;
        }

        return success;
    }

    /// <summary>
    /// Libera recursos
    /// </summary>
    public override void Dispose()
    {
        _timer?.Dispose();
        base.Dispose();
    }

    /// <summary>
    /// Executa monitoramento diário (modo Railway/Cloud)
    /// </summary>
    private async Task RodarMonitoramentoAsync(CancellationToken cancellationToken)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var monitoramento = new MonitoramentoKurier
        {
            DataExecucao = DateTime.UtcNow,
            ModoExecucao = "RUN_ONCE",
            SomenteMonitoramento = true
        };

        try
        {
            _logger.LogInformation("Iniciando monitoramento Railway da API Kurier...");

            // 1. Consultar quantidades (endpoints rápidos)
            _logger.LogInformation("Consultando quantidades da API Kurier...");
            
            var quantidadeDistribuicoes = await _kurierClient.ConsultarQuantidadeDistribuicoesAsync(cancellationToken);
            var quantidadePublicacoes = await _kurierClient.ConsultarQuantidadePublicacoesAsync(cancellationToken);

            monitoramento.QuantidadeDistribuicoes = quantidadeDistribuicoes;
            monitoramento.QuantidadePublicacoes = quantidadePublicacoes;

            _logger.LogInformation("Quantidades obtidas - Distribuições: {Distribuicoes}, Publicações: {Publicacoes}", 
                quantidadeDistribuicoes, quantidadePublicacoes);

            // 2. Obter amostras (apenas resumos, até 10 itens)
            if (quantidadeDistribuicoes > 0)
            {
                _logger.LogInformation("Obtendo amostra de distribuições...");
                var distribuicoes = await _kurierClient.ConsultarDistribuicoesAsync(cancellationToken);
                
                if (distribuicoes.Any())
                {
                    monitoramento.AmostraDistribuicoes = GerarAmostraDistribuicoes(distribuicoes.Take(10));
                    _logger.LogInformation("Amostra de distribuições coletada: {Count} itens", 
                        Math.Min(10, distribuicoes.Count));
                }
            }

            if (quantidadePublicacoes > 0)
            {
                _logger.LogInformation("Obtendo amostra de publicações (apenas resumos)...");
                var publicacoes = await _kurierClient.ConsultarPublicacoesAsync(true, cancellationToken);
                
                if (publicacoes.Any())
                {
                    monitoramento.AmostraPublicacoes = GerarAmostraPublicacoes(publicacoes.Take(10));
                    _logger.LogInformation("Amostra de publicações coletada: {Count} itens", 
                        Math.Min(10, publicacoes.Count));
                }
            }

            stopwatch.Stop();
            monitoramento.TempoExecucaoMs = (int)stopwatch.ElapsedMilliseconds;
            monitoramento.StatusExecucao = "Sucesso";

            _logger.LogInformation("Monitoramento concluído com sucesso em {Tempo}ms", 
                monitoramento.TempoExecucaoMs);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            monitoramento.TempoExecucaoMs = (int)stopwatch.ElapsedMilliseconds;
            monitoramento.StatusExecucao = "Erro";
            monitoramento.MensagemErro = ex.Message;

            _logger.LogError(ex, "Erro durante monitoramento da API Kurier");
        }

        // 3. Salvar resultado na base de dados (Railway PostgreSQL)
        try
        {
            await SalvarMonitoramentoAsync(monitoramento, cancellationToken);
            _logger.LogInformation("Resultado do monitoramento salvo na base de dados");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao salvar resultado do monitoramento");
            
            // Se não conseguiu salvar, pelo menos log do resultado para Railway
            LogRelatorioEstruturado(monitoramento);
        }
    }

    /// <summary>
    /// Salva o resultado do monitoramento na tabela MonitoramentoKurier (Railway PostgreSQL)
    /// </summary>
    private async Task SalvarMonitoramentoAsync(MonitoramentoKurier monitoramento, CancellationToken cancellationToken)
    {
        try
        {
            if (_railwayGateway != null)
            {
                // Salvar no PostgreSQL da Railway
                var sucesso = await _railwayGateway.SalvarMonitoramentoAsync(monitoramento, cancellationToken);
                if (sucesso)
                {
                    _logger.LogInformation("Monitoramento salvo com sucesso no PostgreSQL Railway");
                }
                else
                {
                    _logger.LogWarning("Falha ao salvar monitoramento no PostgreSQL Railway");
                }
            }
            else if (_bennerGateway != null)
            {
                // Fallback para execução local - apenas log
                _logger.LogInformation("Execução local: Monitoramento não será salvo no banco");
            }
            
            // Log estruturado do resultado para ambos os casos
            LogRelatorioEstruturado(monitoramento);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao salvar monitoramento");
        }
    }

    /// <summary>
    /// Registra log estruturado do monitoramento para visualização no Railway
    /// </summary>
    private void LogRelatorioEstruturado(MonitoramentoKurier monitoramento)
    {
        _logger.LogInformation("=== RELATÓRIO DE MONITORAMENTO KURIER ===");
        _logger.LogInformation("Data/Hora: {DataExecucao}", monitoramento.DataExecucao);
        _logger.LogInformation("Modo: {Modo}", monitoramento.ModoExecucao);
        _logger.LogInformation("Status: {Status}", monitoramento.StatusExecucao);
        _logger.LogInformation("Tempo Execução: {Tempo}ms", monitoramento.TempoExecucaoMs);
        _logger.LogInformation("Distribuições Disponíveis: {QtdDist}", monitoramento.QuantidadeDistribuicoes);
        _logger.LogInformation("Publicações Disponíveis: {QtdPub}", monitoramento.QuantidadePublicacoes);
        _logger.LogInformation("Somente Monitoramento: {SomenteMonitoramento}", monitoramento.SomenteMonitoramento);
        
        if (!string.IsNullOrEmpty(monitoramento.AmostraDistribuicoes))
            _logger.LogInformation("Amostra Distribuições: {Amostra}", monitoramento.AmostraDistribuicoes);

        if (!string.IsNullOrEmpty(monitoramento.AmostraPublicacoes))
            _logger.LogInformation("Amostra Publicações: {Amostra}", monitoramento.AmostraPublicacoes);

        if (!string.IsNullOrEmpty(monitoramento.MensagemErro))
            _logger.LogError("Erro: {Erro}", monitoramento.MensagemErro);

        _logger.LogInformation("=== FIM DO RELATÓRIO ===");
    }

    /// <summary>
    /// Gera amostra formatada de distribuições para o relatório
    /// </summary>
    private static string GerarAmostraDistribuicoes(IEnumerable<Distribuicao> distribuicoes)
    {
        var amostras = distribuicoes.Select(d => new
        {
            Processo = d.NumeroProcesso,
            Tipo = d.TipoDistribuicao,
            Destinatario = d.Destinatario,
            Data = d.DataDistribuicao.ToString("dd/MM/yyyy"),
            Tribunal = d.Tribunal
        }).ToList();

        return System.Text.Json.JsonSerializer.Serialize(amostras, new System.Text.Json.JsonSerializerOptions 
        { 
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
    }

    /// <summary>
    /// Gera amostra formatada de publicações para o relatório
    /// </summary>
    private static string GerarAmostraPublicacoes(IEnumerable<Publicacao> publicacoes)
    {
        var amostras = publicacoes.Select(p => new
        {
            Processo = p.NumeroProcesso,
            Tipo = p.TipoPublicacao,
            Titulo = (p.Titulo?.Length > 100) ? p.Titulo[..100] + "..." : p.Titulo,
            Data = p.DataPublicacao.ToString("dd/MM/yyyy"),
            Tribunal = p.Tribunal,
            Categoria = p.Categoria
        }).ToList();

        return System.Text.Json.JsonSerializer.Serialize(amostras, new System.Text.Json.JsonSerializerOptions 
        { 
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
    }
}

/// <summary>
/// Configurações para os jobs da Kurier
/// </summary>
public class KurierJobsSettings
{
    /// <summary>
    /// Intervalo em minutos entre execuções
    /// </summary>
    public int IntervalMinutes { get; set; } = 5;

    /// <summary>
    /// Tamanho da página para consultas na API
    /// </summary>
    public int PageSize { get; set; } = 100;

    /// <summary>
    /// Número de dias para consultar no passado
    /// </summary>
    public int DaysToConsult { get; set; } = 7;

    /// <summary>
    /// Tamanho do lote para confirmações
    /// </summary>
    public int ConfirmationBatchSize { get; set; } = 50;
}

/// <summary>
/// Configurações para monitoramento (modo Railway/Cloud)
/// </summary>
public class MonitoringSettings
{
    /// <summary>
    /// Habilita o modo monitoramento
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Confirma os dados recebidos na Kurier
    /// </summary>
    public bool ConfirmarNaKurier { get; set; } = false;

    /// <summary>
    /// Busca apenas resumos dos dados
    /// </summary>
    public bool FetchResumos { get; set; } = true;

    /// <summary>
    /// Busca dados completos (inteiro teor)
    /// </summary>
    public bool FetchInteiroTeor { get; set; } = false;
}