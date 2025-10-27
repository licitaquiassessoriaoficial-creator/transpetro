using BennerKurierWorker.Application;
using BennerKurierWorker.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;
using Serilog;
using System.Net;
using System.Text.Json;
using static BennerKurierWorker.Application.KurierJobs;

namespace BennerKurierWorker.Worker;

/// <summary>
/// Ponto de entrada do Worker Service para integração completa Benner × Kurier
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        // Configurar ServicePointManager para compatibilidade com sistemas legados
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
        ServicePointManager.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;
        ServicePointManager.Expect100Continue = false;
        ServicePointManager.UseNagleAlgorithm = false;

        // Configurar Serilog antes de tudo
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .WriteTo.File("logs/benner-kurier-.txt", rollingInterval: RollingInterval.Day)
            .CreateBootstrapLogger();

        try
        {
            Log.Information("Iniciando BennerKurierWorker");

            // Verificar se é modo de teste
            if (args.Length > 0 && args[0].Equals("--teste-publicacoes", StringComparison.OrdinalIgnoreCase))
            {
                await ExecutarTestePublicacoesAsync(args);
                return;
            }

            var host = CreateHostBuilder(args).Build();
            await host.RunAsync();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Falha crítica no BennerKurierWorker");
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }

    /// <summary>
    /// Executa teste específico para publicações
    /// </summary>
    private static async Task ExecutarTestePublicacoesAsync(string[] args)
    {
        Log.Information("🧪 === MODO TESTE: PUBLICAÇÕES KURIER ===");

        var host = CreateHostBuilder(new string[0]).Build();

        using var scope = host.Services.CreateScope();
        var kurierClient = scope.ServiceProvider.GetRequiredService<IKurierClient>();
        var scopeFactory = scope.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var jobSettings = scope.ServiceProvider.GetRequiredService<IOptions<KurierJobsSettings>>();
        var monitoringSettings = scope.ServiceProvider.GetRequiredService<IOptions<MonitoringSettings>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<KurierJobs>>();

        // Criar instância do KurierJobs diretamente
        var kurierJobs = new KurierJobs(logger, kurierClient, scopeFactory, configuration, jobSettings, monitoringSettings);

        bool confirmarReal = args.Length > 1 && args[1].Equals("--confirmar", StringComparison.OrdinalIgnoreCase);

        try
        {
            // Teste 1: Conectividade
            Log.Information("📋 Teste 1: Conectividade com Kurier");
            var conectividade = await kurierClient.TestarConexaoKurierAsync();
            Log.Information(conectividade ? "✅ Conectividade OK" : "❌ Conectividade FALHOU");

            if (!conectividade)
            {
                Log.Error("🚫 Testes interrompidos devido à falha de conectividade");
                return;
            }

            // Teste 2: Publicações específicas  
            Log.Information("📋 Teste 2: Funcionalidades de Publicações");
            var testePublicacoes = await kurierJobs.TestarPublicacoesKurierAsync();
            Log.Information(testePublicacoes ? "✅ Publicações OK" : "❌ Publicações FALHARAM");

            // Teste 3: Ingestão
            Log.Information("📋 Teste 3: Ingestão de Publicações (confirmar={ConfirmarReal})", confirmarReal);
            var testeIngestao = await kurierJobs.TestarIngestaoPublicacoesAsync(confirmarReal);
            Log.Information(testeIngestao ? "✅ Ingestão OK" : "❌ Ingestão FALHOU");

            Log.Information("🎉 === TESTE CONCLUÍDO ===");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "💥 Falha crítica no teste de publicações");
        }
    }

    /// <summary>
    /// Configura o host do Worker Service
    /// </summary>
    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                // Adicionar variáveis de ambiente com prefixos específicos
                config.AddEnvironmentVariables();
                
                // Suporte específico para Railway e outras plataformas cloud
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    // Mapear variáveis de ambiente para configurações
                    ["Kurier:BaseUrl"] = Environment.GetEnvironmentVariable("Kurier__BaseUrl"),
                    ["Kurier:Usuario"] = Environment.GetEnvironmentVariable("Kurier__User"),
                    ["Kurier:Senha"] = Environment.GetEnvironmentVariable("Kurier__Pass"),
                    ["Benner:ConnectionString"] = Environment.GetEnvironmentVariable("Benner__ConnectionString"),
                    ["RUN_ONCE"] = Environment.GetEnvironmentVariable("RUN_ONCE"),
                    ["MODE"] = Environment.GetEnvironmentVariable("MODE")
                }.Where(kvp => !string.IsNullOrEmpty(kvp.Value))!);
            })
            .UseWindowsService(options =>
            {
                options.ServiceName = "BennerKurierWorker";
            })
            .UseSerilog((context, services, configuration) => configuration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.File("logs/benner-kurier-.txt", 
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}"))
            .ConfigureServices((hostContext, services) =>
            {
                var configuration = hostContext.Configuration;
                var runOnce = Environment.GetEnvironmentVariable("RUN_ONCE")?.ToLowerInvariant() == "true";
                var mode = Environment.GetEnvironmentVariable("MODE")?.ToLowerInvariant() ?? "ingest";

                Log.Information("Configurando serviços. RUN_ONCE = {RunOnce}, MODE = {Mode}", runOnce, mode);

                // Configurar settings das duas integrações Kurier com suporte a variáveis de ambiente
                services.Configure<KurierSettings>(options =>
                {
                    configuration.GetSection("Kurier").Bind(options);
                    // Override com variáveis de ambiente se existirem
                    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("Kurier__BaseUrl")))
                        options.BaseUrl = Environment.GetEnvironmentVariable("Kurier__BaseUrl")!;
                    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("Kurier__User")))
                        options.Usuario = Environment.GetEnvironmentVariable("Kurier__User")!;
                    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("Kurier__Pass")))
                        options.Senha = Environment.GetEnvironmentVariable("Kurier__Pass")!;
                });

                services.Configure<KurierJuridicoSettings>(options =>
                {
                    configuration.GetSection("KurierJuridico").Bind(options);
                    // Override com variáveis de ambiente se existirem
                    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KurierJuridico__BaseUrl")))
                        options.BaseUrl = Environment.GetEnvironmentVariable("KurierJuridico__BaseUrl")!;
                    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KurierJuridico__User")))
                        options.Usuario = Environment.GetEnvironmentVariable("KurierJuridico__User")!;
                    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KurierJuridico__Pass")))
                        options.Senha = Environment.GetEnvironmentVariable("KurierJuridico__Pass")!;
                });

                services.Configure<BennerSettings>(options =>
                {
                    configuration.GetSection("Benner").Bind(options);
                    
                    // Override com variável de ambiente se existir
                    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("Benner__ConnectionString")))
                        options.ConnectionString = Environment.GetEnvironmentVariable("Benner__ConnectionString")!;
                });

                services.Configure<KurierJobsSettings>(
                    configuration.GetSection("Jobs"));
                
                services.Configure<MonitoringSettings>(
                    configuration.GetSection("Monitoring"));

                // Configurar HttpClient Factory para as duas integrações Kurier
                services.AddHttpClient("KurierDistribuicao", client =>
                {
                    client.Timeout = TimeSpan.FromMinutes(5);
                })
                .AddPolicyHandler(GetRetryPolicy())
                .AddPolicyHandler(GetCircuitBreakerPolicy());

                services.AddHttpClient("KurierJuridico", client =>
                {
                    client.Timeout = TimeSpan.FromMinutes(5);
                })
                .AddPolicyHandler(GetRetryPolicy())
                .AddPolicyHandler(GetCircuitBreakerPolicy());

                // Registrar o KurierClient como Scoped (não mais como HttpClient typed client)
                services.AddScoped<IKurierClient, KurierClient>();

                // Registrar serviços baseado no modo de execução
                if (mode == "monitoring" || runOnce)
                {
                    // Para Railway ou modo monitoramento, usar gateway de monitoramento PostgreSQL
                    services.AddScoped<IRailwayMonitoringGateway, RailwayMonitoringGateway>();
                    Log.Information("Configurado para modo monitoramento (Railway PostgreSQL)");
                }
                else
                {
                    // Para execução local de integração, usar gateway Benner
                    services.AddScoped<IBennerGateway, BennerPostgreSqlGateway>();
                    Log.Information("Configurado para modo integração (Benner PostgreSQL)");
                }
                
                // Configurar o hosted service
                services.AddHostedService<KurierJobs>();

                // Configurar Health Checks (opcional, pode ser desabilitado em produção)
                if (!runOnce && mode != "monitoring")
                {
                    services.AddHealthChecks()
                        .AddCheck<BennerHealthCheck>("benner-database")
                        .AddCheck<KurierHealthCheck>("kurier-api");
                }
            });

    /// <summary>
    /// Política de retry com backoff exponencial
    /// </summary>
    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError() // Trata HttpRequestException e 5XX, 408
            .OrResult(msg => !msg.IsSuccessStatusCode && (int)msg.StatusCode != 401) // Retry para todos exceto 401 (não autorizado)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), // 2, 4, 8 segundos
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    Log.Warning("Tentativa {RetryCount} após {Delay}s devido a: {Exception}",
                        retryCount, timespan.TotalSeconds, outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString());
                });
    }

    /// <summary>
    /// Política de circuit breaker
    /// </summary>
    private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromMinutes(1),
                onBreak: (exception, duration) =>
                {
                    Log.Error("Circuit breaker aberto por {Duration}s devido a: {Exception}",
                        duration.TotalSeconds, exception.Exception?.Message ?? exception.Result?.StatusCode.ToString());
                },
                onReset: () =>
                {
                    Log.Information("Circuit breaker fechado - conexão restaurada");
                });
    }
}

/// <summary>
/// Health check para verificar conectividade com banco Benner
/// </summary>
public class BennerHealthCheck : IHealthCheck
{
    private readonly IBennerGateway _bennerGateway;

    public BennerHealthCheck(IBennerGateway bennerGateway)
    {
        _bennerGateway = bennerGateway;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var isHealthy = await _bennerGateway.TestarConexaoAsync(cancellationToken);
            return isHealthy
                ? HealthCheckResult.Healthy("Banco Benner acessível")
                : HealthCheckResult.Unhealthy("Banco Benner inacessível");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Erro ao verificar banco Benner", ex);
        }
    }
}

/// <summary>
/// Health check para verificar conectividade com API Kurier
/// </summary>
public class KurierHealthCheck : IHealthCheck
{
    private readonly IKurierClient _kurierClient;

    public KurierHealthCheck(IKurierClient kurierClient)
    {
        _kurierClient = kurierClient;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var quantidade = await _kurierClient.ConsultarQuantidadeDistribuicoesAsync(cancellationToken);
            
            return HealthCheckResult.Healthy($"API Kurier acessível - {quantidade} distribuições disponíveis");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Erro ao verificar API Kurier", ex);
        }
    }
}