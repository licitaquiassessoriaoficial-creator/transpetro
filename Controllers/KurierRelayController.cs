using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text;
using System.Net;
using Polly;
using Polly.Extensions.Http;

namespace BennerKurierWorker.Controllers;

/// <summary>
/// Controller para relay HTTP→HTTPS entre Benner e Kurier
/// Permite que o Benner se conecte via HTTP e redireciona para HTTPS da Kurier
/// </summary>
[ApiController]
[Route("api/kurier")]
public class KurierRelayController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<KurierRelayController> _logger;
    private readonly KurierRelaySettings _settings;
    private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;

    public KurierRelayController(
        IHttpClientFactory httpClientFactory,
        ILogger<KurierRelayController> logger,
        IOptions<KurierRelaySettings> settings)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _settings = settings.Value;
        _retryPolicy = CreateRetryPolicy();
    }

    /// <summary>
    /// Endpoint principal de relay para redirecionamento HTTP→HTTPS
    /// Aceita todas as requisições (GET, POST, etc.) e encaminha para a Kurier
    /// </summary>
    [HttpGet("relay")]
    [HttpPost("relay")]
    [HttpPut("relay")]
    [HttpPatch("relay")]
    [HttpDelete("relay")]
    public async Task<IActionResult> RelayAsync()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var requestId = Guid.NewGuid().ToString("N")[..8];
        
        _logger.LogInformation("🔄 [REQ-{RequestId}] Iniciando relay {Method} para Kurier", 
            requestId, Request.Method);

        try
        {
            // Preparar dados da requisição
            var requestBody = await ReadRequestBodyAsync();
            var targetUrl = BuildTargetUrl();
            
            _logger.LogInformation("📡 [REQ-{RequestId}] Redirecionando para: {TargetUrl}", 
                requestId, targetUrl);
            
            // Log dos headers principais (sem credenciais)
            LogRequestHeaders(requestId);

            // Fazer a requisição para a Kurier com retry
            var httpClient = _httpClientFactory.CreateClient("KurierRelay");
            var response = await _retryPolicy.ExecuteAsync(async () =>
            {
                var request = CreateHttpRequest(targetUrl, requestBody);
                return await httpClient.SendAsync(request);
            });

            stopwatch.Stop();
            
            // Log da resposta
            var responseBody = await response.Content.ReadAsStringAsync();
            var responseSize = responseBody.Length;
            
            _logger.LogInformation("✅ [REQ-{RequestId}] Resposta recebida: {StatusCode} | {Duration}ms | {Size} chars", 
                requestId, (int)response.StatusCode, stopwatch.ElapsedMilliseconds, responseSize);

            // Log resumido do conteúdo (primeiros 200 caracteres)
            if (!string.IsNullOrEmpty(responseBody))
            {
                var preview = responseBody.Length > 200 
                    ? responseBody[..200] + "..."
                    : responseBody;
                _logger.LogDebug("📄 [REQ-{RequestId}] Conteúdo: {Preview}", requestId, preview);
            }

            // Tenta transformar a resposta em JSON com campo esperado
            string wrappedJson = responseBody;
            if (response.Content.Headers.ContentType?.MediaType == "application/json")
            {
                if (targetUrl.ToLower().Contains("consultardistribuicoes"))
                {
                    wrappedJson = $"{{\"distribuicoes\":{responseBody}}}";
                }
                else if (targetUrl.ToLower().Contains("consultarpublicacoes"))
                {
                    wrappedJson = $"{{\"publicacoes\":{responseBody}}}";
                }
            }
            var result = new ContentResult
            {
                Content = wrappedJson,
                StatusCode = (int)response.StatusCode,
                ContentType = "application/json"
            };
            return result;
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "🚫 [REQ-{RequestId}] Erro de conectividade após {Duration}ms: {Message}", 
                requestId, stopwatch.ElapsedMilliseconds, ex.Message);
            
            return StatusCode(502, new { 
                error = "Erro de conectividade com Kurier", 
                message = ex.Message,
                requestId 
            });
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "⏰ [REQ-{RequestId}] Timeout após {Duration}ms", 
                requestId, stopwatch.ElapsedMilliseconds);
            
            return StatusCode(504, new { 
                error = "Timeout na comunicação com Kurier", 
                timeout = $"{_settings.TimeoutSeconds}s",
                requestId 
            });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "💥 [REQ-{RequestId}] Erro inesperado após {Duration}ms: {Message}", 
                requestId, stopwatch.ElapsedMilliseconds, ex.Message);
            
            return StatusCode(500, new { 
                error = "Erro interno do relay", 
                message = ex.Message,
                requestId 
            });
        }
    }

    /// <summary>
    /// Endpoint de teste de conectividade
    /// </summary>
    [HttpGet("health")]
    public async Task<IActionResult> HealthCheckAsync()
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient("KurierRelay");
            var response = await httpClient.GetAsync(_settings.BaseUrl);
            
            var isHealthy = response.IsSuccessStatusCode;
            var status = isHealthy ? "healthy" : "unhealthy";
            
            _logger.LogInformation("🏥 Health check Kurier: {Status} ({StatusCode})", 
                status, (int)response.StatusCode);

            return Ok(new 
            {
                status,
                kurier = new 
                {
                    baseUrl = _settings.BaseUrl,
                    statusCode = (int)response.StatusCode,
                    accessible = isHealthy
                },
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "🚫 Health check falhou: {Message}", ex.Message);
            
            return StatusCode(503, new 
            {
                status = "unhealthy",
                error = ex.Message,
                timestamp = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// Lê o corpo da requisição de forma assíncrona
    /// </summary>
    private async Task<string> ReadRequestBodyAsync()
    {
        if (Request.ContentLength == 0 || Request.ContentLength == null)
            return string.Empty;

        Request.EnableBuffering();
        Request.Body.Position = 0;
        
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();
        
        Request.Body.Position = 0; // Reset para próximas leituras
        return body;
    }

    /// <summary>
    /// Constrói a URL de destino na Kurier baseada nos parâmetros da query string
    /// </summary>
    private string BuildTargetUrl()
    {
        var baseUrl = _settings.BaseUrl.TrimEnd('/');
        var queryString = Request.QueryString.HasValue ? Request.QueryString.Value : "";
        
        return $"{baseUrl}/{queryString}";
    }

    /// <summary>
    /// Cria a requisição HTTP para encaminhar à Kurier
    /// </summary>
    private HttpRequestMessage CreateHttpRequest(string targetUrl, string requestBody)
    {
        var request = new HttpRequestMessage(new HttpMethod(Request.Method), targetUrl);

        // Copiar headers relevantes (exceto Host)
        foreach (var header in Request.Headers)
        {
            if (header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase) ||
                header.Key.Equals("Connection", StringComparison.OrdinalIgnoreCase))
                continue;

            request.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
        }

        // Adicionar corpo da requisição se existir
        if (!string.IsNullOrEmpty(requestBody))
        {
            request.Content = new StringContent(requestBody, Encoding.UTF8, 
                Request.ContentType ?? "application/xml");
        }

        return request;
    }

    /// <summary>
    /// Log dos headers principais da requisição (sem credenciais sensíveis)
    /// </summary>
    private void LogRequestHeaders(string requestId)
    {
        var relevantHeaders = Request.Headers
            .Where(h => !h.Key.ToLower().Contains("authorization") && 
                       !h.Key.ToLower().Contains("password"))
            .Take(5)
            .ToDictionary(h => h.Key, h => string.Join(", ", h.Value.ToArray()));

        if (relevantHeaders.Any())
        {
            _logger.LogDebug("📋 [REQ-{RequestId}] Headers: {@Headers}", requestId, relevantHeaders);
        }
    }

    /// <summary>
    /// Cria a política de retry com backoff exponencial
    /// </summary>
    private IAsyncPolicy<HttpResponseMessage> CreateRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => !msg.IsSuccessStatusCode && 
                           (int)msg.StatusCode >= 500) // Retry apenas para erros 5xx
            .WaitAndRetryAsync(
                retryCount: _settings.MaxRetries,
                sleepDurationProvider: retryAttempt => 
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), // 2, 4, 8 segundos
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    var error = outcome.Exception?.Message ?? 
                               $"HTTP {outcome.Result?.StatusCode}";
                    
                    _logger.LogWarning("🔄 Tentativa {RetryCount}/{MaxRetries} após {Delay}s. Erro: {Error}",
                        retryCount, _settings.MaxRetries, timespan.TotalSeconds, error);
                });
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