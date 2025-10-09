using BennerKurierWorker.Application;
using BennerKurierWorker.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Polly;
using Polly.Extensions.Http;
using Serilog;
using static BennerKurierWorker.Application.KurierJobs;

namespace BennerKurierWorker.Worker;

/// <summary>
/// Ponto de entrada do Worker Service
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        // Configurar Serilog antes de tudo
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .WriteTo.File("logs/benner-kurier-.txt", rollingInterval: RollingInterval.Day)
            .CreateBootstrapLogger();

        try
        {
            Log.Information("Iniciando BennerKurierWorker");

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
                    ["Kurier:User"] = Environment.GetEnvironmentVariable("Kurier__User"),
                    ["Kurier:Password"] = Environment.GetEnvironmentVariable("Kurier__Password"),
                    ["Benner:ConnectionString"] = Environment.GetEnvironmentVariable("Benner__ConnectionString"),
                    ["RUN_ONCE"] = Environment.GetEnvironmentVariable("RUN_ONCE")
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

                Log.Information("Configurando serviços. RUN_ONCE = {RunOnce}", runOnce);

                // Configurar settings com suporte a variáveis de ambiente
                services.Configure<KurierSettings>(options =>
                {
                    configuration.GetSection("Kurier").Bind(options);
                    
                    // Override com variáveis de ambiente se existirem
                    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("Kurier__BaseUrl")))
                        options.BaseUrl = Environment.GetEnvironmentVariable("Kurier__BaseUrl")!;
                    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("Kurier__Usuario")))
                        options.Usuario = Environment.GetEnvironmentVariable("Kurier__Usuario")!;
                    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("Kurier__Senha")))
                        options.Senha = Environment.GetEnvironmentVariable("Kurier__Senha")!;
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

                // Configurar HttpClient com Polly para retry policy
                services.AddHttpClient<IKurierClient, KurierClient>(client =>
                {
                    client.Timeout = TimeSpan.FromMinutes(5);
                })
                .AddPolicyHandler(GetRetryPolicy())
                .AddPolicyHandler(GetCircuitBreakerPolicy());

                // Registrar serviços
                services.AddScoped<IBennerGateway, BennerSqlGateway>();
                
                // Configurar o hosted service baseado no modo de execução
                if (runOnce)
                {
                    // Para execução única, usar como serviço transiente
                    services.AddHostedService<KurierJobs>();
                    Log.Information("Configurado para execução única (RUN_ONCE=true)");
                }
                else
                {
                    // Para execução contínua, configuração normal
                    services.AddHostedService<KurierJobs>();
                    Log.Information("Configurado para execução contínua (RUN_ONCE=false)");
                }

                // Configurar Health Checks (opcional, pode ser desabilitado em produção)
                if (!runOnce)
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
            var distribuicoes = await _kurierClient.ConsultarDistribuicoesAsync(cancellationToken);
            
            return HealthCheckResult.Healthy($"API Kurier acessível - {distribuicoes.Count} distribuições encontradas");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Erro ao verificar API Kurier", ex);
        }
    }
}