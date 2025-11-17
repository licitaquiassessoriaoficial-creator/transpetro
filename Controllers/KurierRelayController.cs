using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text;
using System.Net;
using Polly;
using Polly.Extensions.Http;
using System.IO; // Adicionado para StreamReader
using Microsoft.AspNetCore.Http; // Adicionado para EnableBuffering
using System.Net.Http; // Adicionado para HttpClient
using System.Threading.Tasks; // Adicionado para Task
using System.Linq; // Adicionado para LINQ

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
    private readonly TranspetroSettings _transpetroSettings;

    public KurierRelayController(
        IHttpClientFactory httpClientFactory,
        ILogger<KurierRelayController> logger,
        IOptions<KurierRelaySettings> settings,
        IOptions<TranspetroSettings> transpetroSettings)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _settings = settings.Value;
        _retryPolicy = CreateRetryPolicy();
        _transpetroSettings = transpetroSettings.Value;
    }

    /// <summary>
    /// Endpoint para integração: busca dados na Kurier e envia para Transpetro
    /// </summary>
    [HttpPost("integrar")]
    public async Task<IActionResult> IntegrarAsync()
    {
        try
        {
            // Busca dados na Kurier
            var httpClientKurier = _httpClientFactory.CreateClient("KurierRelay");
            var kurierUrl = _settings.BaseUrl.TrimEnd('/') + "/api/KDistribuicao/ConsultarDistribuicoes";
            var kurierResponse = await httpClientKurier.GetAsync(kurierUrl);
            kurierResponse.EnsureSuccessStatusCode();
            var kurierData = await kurierResponse.Content.ReadAsStringAsync();

            // Envia dados para Transpetro
            var httpClientTranspetro = _httpClientFactory.CreateClient("Transpetro");
            var transpetroUrl = _transpetroSettings.BaseUrl.TrimEnd('/') + "/api/receber-dados";
            var content = new StringContent(kurierData, Encoding.UTF8, "application/json");
            var transpetroResponse = await httpClientTranspetro.PostAsync(transpetroUrl, content);
            transpetroResponse.EnsureSuccessStatusCode();
            var transpetroResult = await transpetroResponse.Content.ReadAsStringAsync();

            return Ok(new {
                status = "success",
                kurierDataLength = kurierData.Length,
                transpetroResult
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
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
            // Garante que a resposta seja lida como texto UTF-8
            var responseBytes = await response.Content.ReadAsByteArrayAsync();
            // Detecta e descompacta GZIP se necessário
            if (response.Content.Headers.ContentEncoding.Contains("gzip"))
            {
                using (var compressedStream = new System.IO.MemoryStream(responseBytes))
                using (var gzipStream = new System.IO.Compression.GZipStream(compressedStream, System.IO.Compression.CompressionMode.Decompress))
                using (var resultStream = new System.IO.MemoryStream())
                {
                    await gzipStream.CopyToAsync(resultStream);
                    responseBytes = resultStream.ToArray();
                }
            }
            var responseBody = System.Text.Encoding.UTF8.GetString(responseBytes);
            var responseSize = responseBody.Length;
            
            _logger.LogInformation("✅ [REQ-{RequestId}] Resposta recebida: {StatusCode} | {Duration}ms | {Size} chars", 
                requestId, (int)response.StatusCode, stopwatch.ElapsedMilliseconds, responseSize);

            // Log completo do conteúdo da resposta para diagnóstico
            // (deve ser feito após wrappedJson ser definido)

            // Tenta transformar a resposta em JSON com campo esperado
            string wrappedJson = responseBody;
            // Detecta se é JSON ou XML
            string contentType = response.Content.Headers.ContentType?.MediaType ?? "application/json";
            if (contentType.Contains("json"))
            {
                // Se a resposta estiver vazia ou nula, retorna array vazio
                var isEmpty = string.IsNullOrWhiteSpace(responseBody) || responseBody == "null";
                if (targetUrl.ToLower().Contains("consultardistribuicoes"))
                {
                    wrappedJson = isEmpty ? "{\"distribuicoes\":[]}" : $"{{\"distribuicoes\":{responseBody}}}";
                }
                else if (targetUrl.ToLower().Contains("consultarpublicacoes"))
                {
                    wrappedJson = isEmpty ? "{\"publicacoes\":[]}" : $"{{\"publicacoes\":{responseBody}}}";
                }
                contentType = "application/json";
            }
            else if (contentType.Contains("xml"))
            {
                // Se for XML, apenas retorna como texto
                wrappedJson = responseBody;
                contentType = "application/xml";
            }
            else
            {
                // Se não for reconhecido, retorna como texto puro
                wrappedJson = responseBody;
                contentType = "text/plain";
            }
            var result = new ContentResult
            {
                Content = wrappedJson,
                StatusCode = (int)response.StatusCode,
                ContentType = contentType
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
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(Request.QueryString.Value ?? "");
        if (!query.ContainsKey("tipo") || !query.ContainsKey("metodo"))
        {
            throw new InvalidOperationException("A query string deve conter os parâmetros 'tipo' e 'metodo'. Exemplo: ?tipo=distribuicao&metodo=consultar");
        }

        var tipo = query["tipo"].ToString().ToLower();
        var metodo = query["metodo"].ToString().ToLower();

        string apiUrl = tipo switch
        {
            "distribuicao" => metodo switch
            {
                "consultar" => "api/KDistribuicao/ConsultarDistribuicoes",
                "confirmar" => "api/KDistribuicao/ConfirmarDistribuicoes",
                "quantidade" => "api/KDistribuicao/QuantidadeDistribuicoesDisponiveis",
                "confirmadas" => "api/KDistribuicao/ConsultarDistribuicoesConfirmadas",
                _ => throw new InvalidOperationException("Método inválido para distribuições.")
            },
            "juridico" => metodo switch
            {
                "consultar" => "api/KJuridico/ConsultarPublicacoes",
                "confirmar" => "api/KJuridico/ConfirmarPublicacoes",
                "quantidade" => "api/KJuridico/ConsultarQuantidadePublicacoesDisponiveis",
                "personalizado" => "api/KJuridico/ConsultarPublicacoesPersonalizado",
                _ => throw new InvalidOperationException("Método inválido para jurídico.")
            },
            _ => throw new InvalidOperationException("Tipo inválido. Use 'distribuicao' ou 'juridico'.")
        };

        var baseUrl = _settings.BaseUrl.TrimEnd('/');
        // Monta a URL final, incluindo outros parâmetros
        var extraParams = string.Join("&", query.Where(kvp => kvp.Key != "tipo" && kvp.Key != "metodo").Select(kvp => $"{kvp.Key}={kvp.Value}"));
        var url = string.IsNullOrEmpty(extraParams)
            ? $"{baseUrl}/{apiUrl}"
            : $"{baseUrl}/{apiUrl}?{extraParams}";
        return url;
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

        // Adicionar autenticação básica se configurado
        if (!string.IsNullOrEmpty(_settings.Usuario) && !string.IsNullOrEmpty(_settings.Senha))
        {
            var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_settings.Usuario}:{_settings.Senha}"));
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authValue);
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
    public string BaseUrl { get; set; } = "https://www.kurierservicos.com.br/wsservicos";
    public string Usuario { get; set; } = "o.de.quadro.distribuicao";
    public string Senha { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 100;
    public int MaxRetries { get; set; } = 3;
}

/// <summary>
/// Configurações específicas para a API Transpetro
/// </summary>
public class TranspetroSettings
{
    public string BaseUrl { get; set; } = "https://api.transpetro.com.br/";
    public string Usuario { get; set; } = "";
    public string Senha { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 30;
    public string UserAgent { get; set; } = "BennerKurierWorker/1.0";
}