using BennerKurierWorker.Application;
using BennerKurierWorker.Infrastructure;
using BennerKurierWorker.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;
using Serilog;
using System.Net;
using System.Text.Json;
using static BennerKurierWorker.Application.KurierJobs;

namespace BennerKurierWorker;

/// <summary>
/// Aplicação híbrida: ASP.NET Core (para API relay) + Worker Service (para jobs automáticos)
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
            Log.Information("🚀 Iniciando BennerKurierWorker (Híbrido: API + Worker)");

            // Verificar se é modo de teste
            if (args.Length > 0 && args[0].Equals("--teste-publicacoes", StringComparison.OrdinalIgnoreCase))
            {
                await ExecutarTestePublicacoesAsync(args);
                return;
            }

            var builder = WebApplication.CreateBuilder(args);
            ConfigureServices(builder);
            
            var app = builder.Build();
            ConfigureApp(app);

            await app.RunAsync();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "💥 Falha crítica no BennerKurierWorker");
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }

    /// <summary>
    /// Configura os serviços da aplicação
    /// </summary>
    private static void ConfigureServices(WebApplicationBuilder builder)
    {
        var configuration = builder.Configuration;
        var services = builder.Services;

        // Configurar variáveis de ambiente específicas
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Kurier:BaseUrl"] = Environment.GetEnvironmentVariable("Kurier__BaseUrl"),
            ["Kurier:Usuario"] = Environment.GetEnvironmentVariable("Kurier__User"),
            ["Kurier:Senha"] = Environment.GetEnvironmentVariable("Kurier__Pass"),
            ["Benner:ConnectionString"] = Environment.GetEnvironmentVariable("Benner__ConnectionString"),
            ["RUN_ONCE"] = Environment.GetEnvironmentVariable("RUN_ONCE"),
            ["MODE"] = Environment.GetEnvironmentVariable("MODE"),
            ["PORT"] = Environment.GetEnvironmentVariable("PORT") ?? "8080"
        }.Where(kvp => !string.IsNullOrEmpty(kvp.Value))!);

        // Configurar Serilog
        builder.Host.UseSerilog((context, services, configuration) => configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File("logs/benner-kurier-.txt", 
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}"));

        // Configurar Web Server
        var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
        builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

        // Configurar ASP.NET Core
        services.AddControllers();
        services.AddEndpointsApiExplorer();
        services.AddHealthChecks();

        var runOnce = Environment.GetEnvironmentVariable("RUN_ONCE")?.ToLowerInvariant() == "true";
        var mode = Environment.GetEnvironmentVariable("MODE")?.ToLowerInvariant() ?? "ingest";

        Log.Information("⚙️ Configuração: RUN_ONCE = {RunOnce}, MODE = {Mode}, PORT = {Port}", runOnce, mode, port);

        // Configurar settings principais
        services.Configure<KurierSettings>(options =>
        {
            configuration.GetSection("Kurier").Bind(options);
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
        });

        services.Configure<BennerSettings>(options =>
        {
            configuration.GetSection("Benner").Bind(options);
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("Benner__ConnectionString")))
                options.ConnectionString = Environment.GetEnvironmentVariable("Benner__ConnectionString")!;
        });

        // Configurar settings específicos para o relay
        services.Configure<KurierRelaySettings>(options =>
        {
            configuration.GetSection("KurierRelay").Bind(options);
        });

        services.Configure<KurierJobsSettings>(configuration.GetSection("Jobs"));
        services.Configure<MonitoringSettings>(configuration.GetSection("Monitoring"));

        // Configurar HttpClient Factory
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

        // HttpClient específico para o relay
        services.AddHttpClient("KurierRelay", (serviceProvider, client) =>
        {
            var relaySettings = serviceProvider.GetRequiredService<IOptions<KurierRelaySettings>>().Value;
            client.Timeout = TimeSpan.FromSeconds(relaySettings.TimeoutSeconds);
            client.DefaultRequestHeaders.Add("User-Agent", "BennerKurierWorker-Relay/1.0 (Railway)");
        })
        .AddPolicyHandler(GetRetryPolicy());

        // Registrar serviços
        services.AddScoped<IKurierClient, KurierClient>();

        // Registrar gateway baseado no modo
        if (mode == "integration")
        {
            services.AddScoped<IBennerGateway, BennerPostgreSqlGateway>();
            Log.Information("✅ Gateway configurado: Benner PostgreSQL");
        }
        else
        {
            services.AddScoped<IBennerGateway, BennerPostgreSqlGateway>();
            Log.Information("✅ Gateway configurado: Benner (modo padrão)");
        }

        // Worker Service (apenas se não for modo API puro)
        if (mode != "api-only")
        {
            services.AddHostedService<KurierJobs>();
            Log.Information("✅ Worker Service habilitado");
        }
        else
        {
            Log.Information("⚠️ Worker Service desabilitado (modo API-only)");
        }

        // Health checks
        services.AddHealthChecks()
            .AddCheck<KurierRelayHealthCheck>("kurier-relay");
    }

    /// <summary>
    /// Configura o pipeline da aplicação ASP.NET Core
    /// </summary>
    private static void ConfigureApp(WebApplication app)
    {
        // Middleware de logging de requisições
        app.Use(async (context, next) =>
        {
            var start = DateTime.UtcNow;
            await next();
            var duration = DateTime.UtcNow - start;
            
            Log.Information("🌐 {Method} {Path} -> {StatusCode} ({Duration}ms)",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                duration.TotalMilliseconds);
        });

        // Configurar pipeline
        app.UseRouting();
        app.MapControllers();
        
        // Health checks
        app.MapHealthChecks("/health");
        
        // Endpoint raiz com informações da aplicação
        app.MapGet("/", () => new
        {
            service = "BennerKurierWorker",
            version = "1.0.0",
            mode = Environment.GetEnvironmentVariable("MODE") ?? "ingest",
            status = "running",
            endpoints = new
            {
                relay = "/api/kurier/relay",
                health = "/health",
                kurierHealth = "/api/kurier/health"
            },
            timestamp = DateTime.UtcNow
        });

        Log.Information("🎯 Aplicação configurada. Endpoints disponíveis:");
        Log.Information("   📡 Relay: /api/kurier/relay");
        Log.Information("   🏥 Health: /health");
        Log.Information("   🔍 Kurier Health: /api/kurier/health");
    }

    /// <summary>
    /// Executa teste específico para publicações
    /// </summary>
    private static async Task ExecutarTestePublicacoesAsync(string[] args)
    {
        Log.Information("🧪 === MODO TESTE: PUBLICAÇÕES KURIER ===");

        var builder = WebApplication.CreateBuilder(new string[0]);
        ConfigureServices(builder);
        var app = builder.Build();

        using var scope = app.Services.CreateScope();
        var kurierClient = scope.ServiceProvider.GetRequiredService<IKurierClient>();
        var scopeFactory = scope.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var jobSettings = scope.ServiceProvider.GetRequiredService<IOptions<KurierJobsSettings>>();
        var monitoringSettings = scope.ServiceProvider.GetRequiredService<IOptions<MonitoringSettings>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<KurierJobs>>();

        var kurierJobs = new KurierJobs(logger, kurierClient, scopeFactory, configuration, jobSettings, monitoringSettings);

        bool confirmarReal = args.Length > 1 && args[1].Equals("--confirmar", StringComparison.OrdinalIgnoreCase);

        try
        {
            Log.Information("📋 Teste 1: Conectividade com Kurier");
            var conectividade = await kurierClient.TestarConexaoKurierAsync();
            Log.Information(conectividade ? "✅ Conectividade OK" : "❌ Conectividade FALHOU");

            if (!conectividade)
            {
                Log.Error("🚫 Testes interrompidos devido à falha de conectividade");
                return;
            }

            Log.Information("📋 Teste 2: Funcionalidades de Publicações");
            var testePublicacoes = await kurierJobs.TestarPublicacoesKurierAsync();
            Log.Information(testePublicacoes ? "✅ Publicações OK" : "❌ Publicações FALHARAM");

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
    /// Política de retry com backoff exponencial
    /// </summary>
    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => !msg.IsSuccessStatusCode && (int)msg.StatusCode != 401)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    Log.Warning("🔄 Retry {RetryCount} após {Delay}s: {Exception}",
                        retryCount, timespan.TotalSeconds, 
                        outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString());
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
                    Log.Error("🚨 Circuit breaker aberto por {Duration}s: {Exception}",
                        duration.TotalSeconds, 
                        exception.Exception?.Message ?? exception.Result?.StatusCode.ToString());
                },
                onReset: () =>
                {
                    Log.Information("✅ Circuit breaker restaurado");
                });
    }
}

/// <summary>
/// Health check específico para o relay Kurier
/// </summary>
public class KurierRelayHealthCheck : IHealthCheck
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<KurierRelaySettings> _settings;

    public KurierRelayHealthCheck(IHttpClientFactory httpClientFactory, IOptions<KurierRelaySettings> settings)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient("KurierRelay");
            var response = await httpClient.GetAsync(_settings.Value.BaseUrl, cancellationToken);
            
            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy($"Kurier acessível via {_settings.Value.BaseUrl}")
                : HealthCheckResult.Degraded($"Kurier retornou {response.StatusCode}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Erro ao conectar com Kurier", ex);
        }
    }
}

/// <summary>
/// Configurações específicas para o relay Kurier
/// </summary>
public class KurierRelaySettings
{
    public string BaseUrl { get; set; } = "https://www.kurierservicos.com.br/wsservicos/";
    public int TimeoutSeconds { get; set; } = 100;
    public int MaxRetries { get; set; } = 3;
}