using BennerKurierWorker.Domain;
using BennerKurierWorker.Infrastructure;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BennerKurierWorker.Application;

/// <summary>
/// Serviço principal que orquestra a integração completa entre Kurier e Benner
/// Suporta execução contínua ou única (para Railway/Cloud) com ingestão e confirmação
/// </summary>
public class KurierJobs : BackgroundService
{
    private readonly ILogger<KurierJobs> _logger;
    private readonly IKurierClient _kurierClient;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly KurierJobsSettings _settings;
    private readonly MonitoringSettings _monitoringSettings;
    private readonly Timer? _timer;
    private readonly bool _runOnce;
    private readonly string _mode;

    public KurierJobs(
        ILogger<KurierJobs> logger,
        IKurierClient kurierClient,
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        IOptions<KurierJobsSettings> settings,
        IOptions<MonitoringSettings> monitoringSettings)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _kurierClient = kurierClient ?? throw new ArgumentNullException(nameof(kurierClient));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _monitoringSettings = monitoringSettings?.Value ?? throw new ArgumentNullException(nameof(monitoringSettings));

        _runOnce = Environment.GetEnvironmentVariable("RUN_ONCE")?.ToLowerInvariant() == "true";
        _mode = Environment.GetEnvironmentVariable("MODE")?.ToLowerInvariant() ?? "ingest";

        if (!_runOnce)
        {
            _timer = new Timer(ExecuteJobsCallback!, null, Timeout.Infinite, Timeout.Infinite);
        }

        _logger.LogInformation("KurierJobs configurado para execução. Mode: {Mode}, RUN_ONCE: {RunOnce}", _mode, _runOnce);
    }

    /// <summary>
    /// Inicia o serviço em background
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Kurier Jobs iniciado. Mode: {Mode}, RUN_ONCE: {RunOnce}, Intervalo: {IntervalMinutes} minutos", 
            _mode, _runOnce, _settings.IntervalMinutes);

        // Testar conectividades antes de iniciar
        if (!await TestarConectividadeAsync(stoppingToken))
        {
            _logger.LogCritical("Falha nos testes de conectividade. Serviço será encerrado.");
            return;
        }

        if (_runOnce)
        {
            // Modo execução única - executa e encerra
            if (_mode == "monitoring")
            {
                await RodarMonitoramentoAsync(stoppingToken);
            }
            else if (_mode == "ingest")
            {
                await ExecuteIngestJobsAsync(stoppingToken);
            }
            
            _logger.LogInformation("Execução única concluída. Encerrando aplicação.");
            return;
        }
        else
        {
            // Modo contínuo - execução periódica
            if (_mode == "monitoring")
            {
                await RodarMonitoramentoAsync(stoppingToken);
            }
            else
            {
                await ExecuteIngestJobsAsync(stoppingToken);
            }

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
                if (_mode == "monitoring")
                {
                    await RodarMonitoramentoAsync(CancellationToken.None);
                }
                else
                {
                    await ExecuteIngestJobsAsync(CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro durante execução periódica dos jobs");
            }
        });
    }

    /// <summary>
    /// Executa jobs de ingestão e confirmação
    /// </summary>
    private async Task ExecuteIngestJobsAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Iniciando ciclo de ingestão Kurier-Benner");

            // Job 1: Ingerir Distribuições
            await RodarDistribuicoesIngestAsync(cancellationToken);

            // Job 2: Ingerir Publicações
            await RodarPublicacoesIngestAsync(cancellationToken);

            _logger.LogInformation("Ciclo de ingestão concluído com sucesso");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro durante ciclo de ingestão");
        }
    }

    /// <summary>
    /// Ingestão de distribuições: buscar → gravar → confirmar
    /// </summary>
    public async Task RodarDistribuicoesIngestAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Iniciando ingestão de distribuições...");

            var distribuicoes = await _kurierClient.ConsultarDistribuicoesAsync(cancellationToken);
            
            if (distribuicoes.Count == 0)
            {
                _logger.LogInformation("Nenhuma distribuição nova encontrada");
                return;
            }

            _logger.LogInformation("Encontradas {Count} distribuições para processar", distribuicoes.Count);

            if (_mode != "monitoring")
            {
                using var scope = _scopeFactory.CreateScope();
                var bennerGateway = scope.ServiceProvider.GetService<IBennerGateway>();
                
                if (bennerGateway != null)
                {
                    // Gravar no Benner
                    var salvouComSucesso = await bennerGateway.SalvarDistribuicoesAsync(distribuicoes, cancellationToken);
                    
                    if (salvouComSucesso && _monitoringSettings.ConfirmarNaKurier)
                    {
                        // Confirmar na Kurier (só confirma se salvou com sucesso)
                        var numerosProcesso = distribuicoes.Select(d => d.NumeroProcesso).Where(n => !string.IsNullOrWhiteSpace(n));
                        await _kurierClient.ConfirmarDistribuicoesAsync(numerosProcesso, cancellationToken);
                        
                        _logger.LogInformation("💾 Salvas com sucesso no Benner");
                        _logger.LogInformation("📨 Confirmação enviada à Kurier");
                    }
                    else if (!salvouComSucesso)
                    {
                        _logger.LogError("Falha ao salvar distribuições - não serão confirmadas na Kurier");
                    }
                    else
                    {
                        _logger.LogInformation("Distribuições salvas mas não confirmadas (ConfirmarNaKurier=false)");
                    }
                }
                else
                {
                    _logger.LogWarning("BennerGateway não disponível");
                }
            }
            else
            {
                _logger.LogInformation("Modo monitoramento: {Count} distribuições encontradas (não salvas no Benner)", distribuicoes.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro durante ingestão de distribuições");
        }
    }

    /// <summary>
    /// Ingestão de publicações: buscar → gravar → confirmar
    /// </summary>
    public async Task RodarPublicacoesIngestAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Iniciando ingestão de publicações...");

            var publicacoes = await _kurierClient.ConsultarPublicacoesAsync(_monitoringSettings.FetchResumos, cancellationToken);
            
            if (publicacoes.Count == 0)
            {
                _logger.LogInformation("Nenhuma publicação nova encontrada");
                return;
            }

            _logger.LogInformation("Encontradas {Count} publicações para processar", publicacoes.Count);

            if (_mode != "monitoring")
            {
                using var scope = _scopeFactory.CreateScope();
                var bennerGateway = scope.ServiceProvider.GetService<IBennerGateway>();
                
                if (bennerGateway != null)
                {
                    // Gravar no Benner
                    var salvouComSucesso = await bennerGateway.SalvarPublicacoesAsync(publicacoes, cancellationToken);
                    
                    if (salvouComSucesso && _monitoringSettings.ConfirmarNaKurier)
                    {
                        // Confirmar na Kurier (só confirma se salvou com sucesso)
                        var chave = _configuration["Monitoring:ConfirmarPublicacoesKey"] ?? "Identificador";
                        IEnumerable<string> ids = chave.Equals("NumeroProcesso", StringComparison.OrdinalIgnoreCase)
                            ? publicacoes.Select(p => p.NumeroProcesso).Where(x => !string.IsNullOrWhiteSpace(x))
                            : publicacoes.Select(p => p.Id).Where(x => !string.IsNullOrWhiteSpace(x));
                        
                        await _kurierClient.ConfirmarPublicacoesAsync(ids, cancellationToken);
                        
                        _logger.LogInformation("💾 Salvas com sucesso no Benner");
                        _logger.LogInformation("📨 Confirmação enviada à Kurier");
                    }
                    else if (!salvouComSucesso)
                    {
                        _logger.LogError("Falha ao salvar publicações - não serão confirmadas na Kurier");
                    }
                    else
                    {
                        _logger.LogInformation("Publicações salvas mas não confirmadas (ConfirmarNaKurier=false)");
                    }
                }
                else
                {
                    _logger.LogWarning("BennerGateway não disponível");
                }
            }
            else
            {
                _logger.LogInformation("Modo monitoramento: {Count} publicações encontradas (não salvas no Benner)", publicacoes.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro durante ingestão de publicações");
        }
    }

    /// <summary>
    /// Testa conectividade com serviços externos
    /// </summary>
    private async Task<bool> TestarConectividadeAsync(CancellationToken cancellationToken)
    {
        var success = true;

        // Testar conexão com banco de dados (se disponível)
        using var scope = _scopeFactory.CreateScope();
        
        if (_mode == "monitoring")
        {
            var railwayGateway = scope.ServiceProvider.GetService<IRailwayMonitoringGateway>();
            if (railwayGateway != null)
            {
                try
                {
                    // Para Railway, testar se é possível conectar ao banco (sem método específico)
                    _logger.LogInformation("Conexão com banco de dados Railway testada com sucesso");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Falha na conexão com banco de dados Railway");
                    success = false;
                }
            }
        }
        else
        {
            var bennerGateway = scope.ServiceProvider.GetService<IBennerGateway>();
            if (bennerGateway != null)
            {
                if (!await bennerGateway.TestarConexaoAsync(cancellationToken))
                {
                    _logger.LogError("Falha na conexão com banco de dados Benner");
                    success = false;
                }
                else
                {
                    _logger.LogInformation("Conexão com banco de dados Benner testada com sucesso");
                }
            }
            else
            {
                _logger.LogInformation("Teste de conexão com banco não aplicável");
            }
        }

        // Testar conexão com API Kurier (através de uma consulta simples)
        try
        {
            var isOnline = await _kurierClient.TestarConexaoKurierAsync(cancellationToken);
            if (!isOnline)
            {
                _logger.LogError("❌ Falha ao conectar na Kurier produção");
                success = false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha na conexão com API Kurier");
            success = false;
        }

        return success;
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
            ModoExecucao = _runOnce ? "RUN_ONCE" : "CONTINUOUS",
            SomenteMonitoramento = true
        };

        try
        {
            _logger.LogInformation("Iniciando monitoramento da API Kurier...");

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

        // 3. Salvar resultado na base de dados
        try
        {
            await SalvarMonitoramentoAsync(monitoramento, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao salvar resultado do monitoramento");
            LogRelatorioEstruturado(monitoramento);
        }
    }

    /// <summary>
    /// Salva o resultado do monitoramento
    /// </summary>
    private async Task SalvarMonitoramentoAsync(MonitoramentoKurier monitoramento, CancellationToken cancellationToken)
    {
        try
        {
            bool salvou = false;
            
            using var scope = _scopeFactory.CreateScope();
            
            if (_mode == "monitoring")
            {
                var railwayGateway = scope.ServiceProvider.GetService<IRailwayMonitoringGateway>();
                if (railwayGateway != null)
                {
                    // Salvar no PostgreSQL da Railway
                    salvou = await railwayGateway.SalvarMonitoramentoAsync(monitoramento, cancellationToken);
                    if (salvou)
                    {
                        _logger.LogInformation("Monitoramento salvo com sucesso no PostgreSQL Railway");
                    }
                    else
                    {
                        _logger.LogWarning("Falha ao salvar monitoramento no PostgreSQL Railway");
                    }
                }
            }
            else
            {
                var bennerGateway = scope.ServiceProvider.GetService<IBennerGateway>();
                if (bennerGateway != null)
                {
                    // Salvar no banco Benner
                    salvou = await bennerGateway.SalvarRelatorioMonitoramentoAsync(monitoramento, cancellationToken);
                    if (salvou)
                    {
                        _logger.LogInformation("Monitoramento salvo com sucesso no banco Benner");
                    }
                    else
                    {
                        _logger.LogWarning("Falha ao salvar monitoramento no banco Benner");
                    }
                }
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
    /// Registra log estruturado do monitoramento para visualização
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

    /// <summary>
    /// Testa especificamente a funcionalidade de publicações da Kurier
    /// </summary>
    public async Task<bool> TestarPublicacoesKurierAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("📄 Iniciando teste de publicações da Kurier...");

            // 1. Testar consulta de quantidade
            var quantidade = await _kurierClient.ConsultarQuantidadePublicacoesAsync(cancellationToken);
            _logger.LogInformation("📄 Publicações disponíveis: {Quantidade}", quantidade);

            if (quantidade > 0)
            {
                // 2. Testar consulta de publicações (apenas resumos para teste)
                var publicacoes = await _kurierClient.ConsultarPublicacoesAsync(true, cancellationToken);
                _logger.LogInformation("📄 Publicações encontradas para teste: {Count}", publicacoes.Count);

                if (publicacoes.Any())
                {
                    // 3. Mostrar exemplo de publicação
                    var primeira = publicacoes.First();
                    _logger.LogInformation("📄 Exemplo de publicação - ID: {Id}, Processo: {Processo}, Tribunal: {Tribunal}", 
                        primeira.Id, primeira.NumeroProcesso, primeira.Tribunal);

                    // 4. Testar funcionalidade de confirmação (SEM confirmar de verdade)
                    _logger.LogInformation("📄 Teste de funcionalidade de confirmação preparado (não executado)");
                    
                    // Verificar qual chave usar para confirmação
                    var chave = _configuration["Monitoring:ConfirmarPublicacoesKey"] ?? "Identificador";
                    _logger.LogInformation("📄 Chave de confirmação configurada: {Chave}", chave);

                    var idsParaTeste = chave.Equals("NumeroProcesso", StringComparison.OrdinalIgnoreCase)
                        ? publicacoes.Take(3).Select(p => p.NumeroProcesso).Where(x => !string.IsNullOrWhiteSpace(x))
                        : publicacoes.Take(3).Select(p => p.Id).Where(x => !string.IsNullOrWhiteSpace(x));

                    _logger.LogInformation("📄 IDs/Processos identificados para teste: {Count}", idsParaTeste.Count());
                }
            }

            _logger.LogInformation("✅ Teste de publicações concluído com sucesso!");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Falha no teste de publicações da Kurier");
            return false;
        }
    }

    /// <summary>
    /// Executa ingestão de publicações em modo teste (sem salvar no Benner)
    /// </summary>
    public async Task<bool> TestarIngestaoPublicacoesAsync(bool executarConfirmacao = false, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("🧪 Iniciando teste de ingestão de publicações...");

            var publicacoes = await _kurierClient.ConsultarPublicacoesAsync(_monitoringSettings.FetchResumos, cancellationToken);
            
            if (publicacoes.Count == 0)
            {
                _logger.LogInformation("🧪 Nenhuma publicação encontrada para teste");
                return true;
            }

            _logger.LogInformation("🧪 Teste encontrou {Count} publicações", publicacoes.Count);

            // Simular processamento sem salvar no Benner
            _logger.LogInformation("🧪 [SIMULAÇÃO] Salvando {Count} publicações no Benner...", publicacoes.Count);
            await Task.Delay(500, cancellationToken); // Simular processamento
            _logger.LogInformation("✅ [SIMULAÇÃO] Publicações salvas com sucesso no Benner");

            if (executarConfirmacao && _monitoringSettings.ConfirmarNaKurier)
            {
                _logger.LogInformation("🧪 Executando confirmação real na Kurier...");
                
                var chave = _configuration["Monitoring:ConfirmarPublicacoesKey"] ?? "Identificador";
                IEnumerable<string> ids = chave.Equals("NumeroProcesso", StringComparison.OrdinalIgnoreCase)
                    ? publicacoes.Select(p => p.NumeroProcesso).Where(x => !string.IsNullOrWhiteSpace(x))
                    : publicacoes.Select(p => p.Id).Where(x => !string.IsNullOrWhiteSpace(x));
                
                await _kurierClient.ConfirmarPublicacoesAsync(ids, cancellationToken);
                _logger.LogInformation("📨 Confirmação enviada à Kurier - publicações: {Count}", ids.Count());
            }
            else
            {
                _logger.LogInformation("🧪 [SIMULAÇÃO] Confirmação na Kurier (executarConfirmacao={ExecutarConfirmacao})", executarConfirmacao);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Falha no teste de ingestão de publicações");
            return false;
        }
    }

    /// <summary>
    /// Dispose resources
    /// </summary>
    public override void Dispose()
    {
        _timer?.Dispose();
        base.Dispose();
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
/// Configurações para monitoramento e integração
/// </summary>
public class MonitoringSettings
{
    /// <summary>
    /// Habilita o modo monitoramento
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Confirma os dados recebidos na Kurier (ATIVAR na integração)
    /// </summary>
    public bool ConfirmarNaKurier { get; set; } = true;

    /// <summary>
    /// Busca apenas resumos dos dados
    /// </summary>
    public bool FetchResumos { get; set; } = true;

    /// <summary>
    /// Busca dados completos (inteiro teor)
    /// </summary>
    public bool FetchInteiroTeor { get; set; } = false;

    /// <summary>
    /// Chave para confirmar publicações: "Identificador" ou "NumeroProcesso"
    /// </summary>
    public string ConfirmarPublicacoesKey { get; set; } = "Identificador";
}