using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using BennerKurierWorker.Domain;
using Polly;
using Polly.Extensions.Http;

namespace BennerKurierWorker.Infrastructure;

/// <summary>
/// Cliente para comunicação com duas integrações independentes da Kurier:
/// 1. Kurier Distribuição (KDistribuicao) - Distribuições judiciais
/// 2. Kurier Jurídico (KJuridico) - Publicações oficiais
/// </summary>
public class KurierClient : IKurierClient, IDisposable
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly HttpClient _httpDistribuicao;
    private readonly HttpClient _httpJuridico;
    private readonly KurierSettings _settingsDistribuicao;
    private readonly KurierJuridicoSettings _settingsJuridico;
    private readonly ILogger<KurierClient> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;

    public KurierClient(
        IHttpClientFactory httpClientFactory,
        IOptions<KurierSettings> settingsDistribuicao,
        IOptions<KurierJuridicoSettings> settingsJuridico,
        ILogger<KurierClient> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _settingsDistribuicao = settingsDistribuicao?.Value ?? throw new ArgumentNullException(nameof(settingsDistribuicao));
        _settingsJuridico = settingsJuridico?.Value ?? throw new ArgumentNullException(nameof(settingsJuridico));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        // Configurar política de retry com Polly
        _retryPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => !msg.IsSuccessStatusCode && (int)msg.StatusCode != 401)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    _logger.LogWarning("🔄 Tentativa {RetryCount} após {Delay}s devido a: {Error}",
                        retryCount, timespan.TotalSeconds, 
                        outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString());
                });

        // Criar e configurar HttpClients independentes
        _httpDistribuicao = ConfigurarHttpClientDistribuicao();
        _httpJuridico = ConfigurarHttpClientJuridico();
    }

    /// <summary>
    /// Configura HttpClient específico para Kurier Distribuição
    /// </summary>
    private HttpClient ConfigurarHttpClientDistribuicao()
    {
        var client = _httpClientFactory.CreateClient("KurierDistribuicao");
        client.BaseAddress = new Uri(_settingsDistribuicao.BaseUrl);
        client.Timeout = TimeSpan.FromSeconds(_settingsDistribuicao.TimeoutSeconds);

        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{_settingsDistribuicao.Usuario}:{_settingsDistribuicao.Senha}"));
        client.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Basic", credentials);

        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.Add("User-Agent", _settingsDistribuicao.UserAgent);
        
        _logger.LogInformation("🔵 Kurier Distribuição configurada: {BaseUrl} (User: {Usuario})", 
            _settingsDistribuicao.BaseUrl, _settingsDistribuicao.Usuario);
        
        return client;
    }

    /// <summary>
    /// Configura HttpClient específico para Kurier Jurídico
    /// </summary>
    private HttpClient ConfigurarHttpClientJuridico()
    {
        var client = _httpClientFactory.CreateClient("KurierJuridico");
        client.BaseAddress = new Uri(_settingsJuridico.BaseUrl);
        client.Timeout = TimeSpan.FromSeconds(_settingsJuridico.TimeoutSeconds);

        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{_settingsJuridico.Usuario}:{_settingsJuridico.Senha}"));
        client.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Basic", credentials);

        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.Add("User-Agent", _settingsJuridico.UserAgent);
        
        _logger.LogInformation("🟣 Kurier Jurídico configurado: {BaseUrl} (User: {Usuario})", 
            _settingsJuridico.BaseUrl, _settingsJuridico.Usuario);
        
        return client;
    }

    /// <summary>
    /// Testa conectividade com ambas as integrações Kurier (Distribuição e Jurídico)
    /// </summary>
    public async Task<bool> TestarConexaoKurierAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("🔍 Testando conectividade com ambas as integrações Kurier...");
            
            // Teste 1: Kurier Distribuição
            var qtdDistribuicoes = await ConsultarQuantidadeDistribuicoesAsync(cancellationToken);
            _logger.LogInformation("✅ Kurier Distribuição: {Qtd} distribuições disponíveis", qtdDistribuicoes);
            
            // Teste 2: Kurier Jurídico
            var qtdPublicacoes = await ConsultarQuantidadePublicacoesAsync(cancellationToken);
            _logger.LogInformation("✅ Kurier Jurídico: {Qtd} publicações disponíveis", qtdPublicacoes);
            
            _logger.LogInformation("🎉 Ambas as integrações Kurier funcionando corretamente");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Falha ao conectar nas integrações Kurier");
            return false;
        }
    }

    /// <summary>
    /// Consulta quantidade de distribuições disponíveis para consumo na Kurier Distribuição
    /// Implementa estratégia de fallback com múltiplas rotas
    /// </summary>
    public async Task<int> ConsultarQuantidadeDistribuicoesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("🔵 Consultando quantidade de distribuições na Kurier (produção)...");

        // Lista de endpoints para tentar em ordem de prioridade (sem fallback longo)
        var endpoints = new[]
        {
            "api/KDistribuicao/QuantidadeDistribuicoesDisponiveis"  // Endpoint que funciona
        };

        // Tentativas com endpoint direto que sabemos que funciona
        foreach (var endpoint in endpoints)
        {
            try
            {
                var fullUrl = new Uri(_httpDistribuicao.BaseAddress!, endpoint).ToString();
                _logger.LogInformation("🔍 Tentando rota: {Endpoint}", endpoint);

                var response = await _httpDistribuicao.GetAsync(endpoint, cancellationToken);
                _logger.LogInformation("📡 Status retornado: {StatusCode} para {Endpoint}", response.StatusCode, endpoint);

                if (response.IsSuccessStatusCode)
                {
                    var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    
                    if (int.TryParse(jsonContent.Trim('"'), out var quantidade))
                    {
                        _logger.LogInformation("✅ Quantidade obtida via {Endpoint}: {Quantidade} distribuições", endpoint, quantidade);
                        return quantidade;
                    }

                    _logger.LogWarning("⚠️ Resposta inválida de {Endpoint}: {Content}", endpoint, jsonContent);
                    continue;
                }

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogWarning("⚠️ Endpoint não encontrado (404): {Endpoint} - tentando próxima rota", endpoint);
                    continue;
                }

                // Outros status codes (4xx/5xx) não são fallback, são erros reais
                _logger.LogError("❌ Erro HTTP {StatusCode} em {Endpoint}: {FullUrl}", response.StatusCode, endpoint, fullUrl);
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new HttpRequestException($"Erro HTTP {response.StatusCode} ao consultar {fullUrl}: {errorContent}");
            }
            catch (HttpRequestException)
            {
                throw; // Re-lançar erros HTTP para não mascarar problemas reais
            }
            catch (TaskCanceledException ex) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("⏰ Timeout ao consultar {Endpoint}", endpoint);
                throw new HttpRequestException($"Timeout ao consultar {endpoint}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro inesperado ao consultar {Endpoint}", endpoint);
                continue; // Tentar próximo endpoint
            }
        }

        // Fallback: consultar lista completa e contar itens
        _logger.LogInformation("🔄 Iniciando fallback: consultando lista de distribuições para contar itens");
        
        try
        {
            const string fallbackEndpoint = "api/KDistribuicao/ConsultarDistribuicoes";
            var fallbackUrl = new Uri(_httpDistribuicao.BaseAddress!, fallbackEndpoint).ToString();
            _logger.LogInformation("🔍 Fallback - Tentando rota: {Endpoint}", fallbackEndpoint);

            var response = await _retryPolicy.ExecuteAsync(async () =>
            {
                return await _httpDistribuicao.GetAsync(fallbackEndpoint, cancellationToken);
            });

            _logger.LogInformation("📡 Fallback - Status retornado: {StatusCode} para {Endpoint}", response.StatusCode, fallbackEndpoint);

            if (response.IsSuccessStatusCode)
            {
                var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var distribuicoes = JsonSerializer.Deserialize<List<Distribuicao>>(jsonContent, _jsonOptions) 
                    ?? new List<Distribuicao>();

                var quantidade = distribuicoes.Count;
                _logger.LogInformation("✅ Fallback bem-sucedido: {Quantidade} distribuições encontradas via consulta de lista", quantidade);
                return quantidade;
            }

            _logger.LogError("❌ Fallback falhou - Status: {StatusCode} para {FallbackUrl}", response.StatusCode, fallbackUrl);
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"Fallback falhou - Erro HTTP {response.StatusCode} ao consultar {fallbackUrl}: {errorContent}");
        }
        catch (HttpRequestException)
        {
            throw; // Re-lançar para manter mensagem de erro clara
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro crítico no fallback ao consultar distribuições");
            var fallbackUrl = new Uri(_httpDistribuicao.BaseAddress!, "api/KDistribuicao/ConsultarDistribuicoes").ToString();
            throw new HttpRequestException($"Erro crítico no fallback ao consultar {fallbackUrl}", ex);
        }
    }

    /// <summary>
    /// Consulta novas distribuições pendentes (sem filtro) na Kurier Distribuição
    /// GET /api/KDistribuicao/ConsultarDistribuicoes
    /// </summary>
    public async Task<IReadOnlyList<Distribuicao>> ConsultarDistribuicoesAsync(CancellationToken cancellationToken = default)
    {
        const string endpoint = "api/KDistribuicao/ConsultarDistribuicoes";
        
        try
        {
            _logger.LogInformation("🔵 Consultando distribuições na Kurier (produção)...");
            
            var response = await _retryPolicy.ExecuteAsync(async () =>
            {
                return await _httpDistribuicao.GetAsync(endpoint, cancellationToken);
            });

            response.EnsureSuccessStatusCode();
            var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
            
            var distribuicoes = JsonSerializer.Deserialize<List<Distribuicao>>(jsonContent, _jsonOptions) 
                ?? new List<Distribuicao>();

            _logger.LogInformation("📦 Distribuições encontradas: {Count}", distribuicoes.Count);
            return distribuicoes.AsReadOnly();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "❌ Erro de rede ao consultar distribuições pendentes");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro ao consultar distribuições pendentes");
            throw;
        }
    }

    /// <summary>
    /// Confirma leitura de distribuições na Kurier Distribuição
    /// POST /api/KDistribuicao/ConfirmarDistribuicoes
    /// Payload: { "NumeroProcesso": [ "0000000-00.0000.0.00.0000", ... ] }
    /// </summary>
    public async Task ConfirmarDistribuicoesAsync(IEnumerable<string> numerosProcesso, CancellationToken cancellationToken = default)
    {
        const string endpoint = "api/KDistribuicao/ConfirmarDistribuicoes";
        
        try
        {
            var numeros = numerosProcesso.ToList();
            
            if (!numeros.Any())
            {
                _logger.LogInformation("Nenhum número de processo para confirmar distribuições");
                return;
            }

            _logger.LogInformation("📨 Confirmando {Count} distribuições...", numeros.Count);

            var payload = new { NumeroProcesso = numeros };
            var jsonPayload = JsonSerializer.Serialize(payload, _jsonOptions);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var response = await _retryPolicy.ExecuteAsync(async () =>
            {
                return await _httpDistribuicao.PostAsync(endpoint, content, cancellationToken);
            });

            response.EnsureSuccessStatusCode();
            _logger.LogInformation("🟩 Confirmação enviada à Kurier (Distribuição): {Count} distribuições", numeros.Count);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "❌ Erro de rede ao confirmar distribuições");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro ao confirmar distribuições");
            throw;
        }
    }

    /// <summary>
    /// Consulta distribuições já confirmadas por período na Kurier Distribuição
    /// GET /api/KDistribuicao/ConsultarDistribuicoesConfirmadas?tipoFiltro={DATA_CONSUMO|DATA_DISTRIBUICAO}&dataInicial=yyyy-MM-dd&dataFinal=yyyy-MM-dd
    /// </summary>
    public async Task<IReadOnlyList<Distribuicao>> ConsultarDistribuicoesConfirmadasAsync(
        string tipoFiltro, 
        DateTime dataInicial, 
        DateTime dataFinal, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("🔵 Consultando distribuições confirmadas: {TipoFiltro} de {DataInicial:yyyy-MM-dd} a {DataFinal:yyyy-MM-dd}", 
                tipoFiltro, dataInicial, dataFinal);

            var query = $"?tipoFiltro={tipoFiltro}&dataInicial={dataInicial:yyyy-MM-dd}&dataFinal={dataFinal:yyyy-MM-dd}";
            var endpoint = $"api/KDistribuicao/ConsultarDistribuicoesConfirmadas{query}";

            var response = await _retryPolicy.ExecuteAsync(async () =>
            {
                return await _httpDistribuicao.GetAsync(endpoint, cancellationToken);
            });

            response.EnsureSuccessStatusCode();
            var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
            
            var distribuicoes = JsonSerializer.Deserialize<List<Distribuicao>>(jsonContent, _jsonOptions) 
                ?? new List<Distribuicao>();

            _logger.LogInformation("📦 Encontradas {Count} distribuições confirmadas", distribuicoes.Count);
            return distribuicoes.AsReadOnly();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "❌ Erro de rede ao consultar distribuições confirmadas");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro ao consultar distribuições confirmadas");
            throw;
        }
    }

    /// <summary>
    /// Consulta quantidade de publicações disponíveis na Kurier Jurídico
    /// GET /api/KJuridico/ConsultarQuantidadePublicacoesDisponiveis
    /// </summary>
    public async Task<int> ConsultarQuantidadePublicacoesAsync(CancellationToken cancellationToken = default)
    {
        const string endpoint = "api/KJuridico/ConsultarQuantidadePublicacoesDisponiveis";
        
        try
        {
            _logger.LogInformation("🟣 Consultando publicações na Kurier Jurídico (produção)...");
            
            var response = await _retryPolicy.ExecuteAsync(async () =>
            {
                return await _httpJuridico.GetAsync(endpoint, cancellationToken);
            });

            response.EnsureSuccessStatusCode();
            var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
            
            if (int.TryParse(jsonContent.Trim('"'), out var quantidade))
            {
                _logger.LogInformation("📜 Publicações encontradas: {Quantidade}", quantidade);
                return quantidade;
            }

            _logger.LogWarning("Resposta inválida para quantidade de publicações: {Content}", jsonContent);
            return 0;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "❌ Erro de rede ao consultar quantidade de publicações");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro ao consultar quantidade de publicações");
            throw;
        }
    }

    /// <summary>
    /// Consulta publicações pendentes (até 50 por requisição) na Kurier Jurídico
    /// GET /api/KJuridico/ConsultarPublicacoes
    /// </summary>
    /// <param name="somenteResumos">Se true, busca apenas resumos; se false, inteiro teor</param>
    public async Task<IReadOnlyList<Publicacao>> ConsultarPublicacoesAsync(bool somenteResumos = true, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("🟣 Consultando publicações na Kurier Jurídico (produção) - Resumos: {SomenteResumos}...", somenteResumos);
            
            var query = somenteResumos ? "?somenteResumos=true" : "";
            var endpoint = $"api/KJuridico/ConsultarPublicacoes{query}";

            var response = await _retryPolicy.ExecuteAsync(async () =>
            {
                return await _httpJuridico.GetAsync(endpoint, cancellationToken);
            });

            response.EnsureSuccessStatusCode();
            var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
            
            var publicacoes = JsonSerializer.Deserialize<List<Publicacao>>(jsonContent, _jsonOptions) 
                ?? new List<Publicacao>();

            _logger.LogInformation("📜 Publicações encontradas: {Count}", publicacoes.Count);
            return publicacoes.AsReadOnly();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "❌ Erro de rede ao consultar publicações pendentes");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro ao consultar publicações pendentes");
            throw;
        }
    }

    /// <summary>
    /// Confirma leitura de publicações na Kurier Jurídico
    /// POST /api/KJuridico/ConfirmarPublicacoes
    /// A chave pode ser "Identificador" ou "NumeroProcesso" conforme conta/contrato
    /// </summary>
    public async Task ConfirmarPublicacoesAsync(IEnumerable<string> idsOuNumerosProcesso, CancellationToken cancellationToken = default)
    {
                const string endpoint = "api/KPublicacao/ConfirmarPublicacoes";
        
        try
        {
            var ids = idsOuNumerosProcesso.ToList();
            
            if (!ids.Any())
            {
                _logger.LogInformation("Nenhum ID para confirmar publicações");
                return;
            }

            _logger.LogInformation("📨 Confirmando {Count} publicações...", ids.Count);

            var chaveConfirmacao = _settingsJuridico.ConfirmarPublicacoesKey ?? "Identificador";
            var payload = new Dictionary<string, object> { [chaveConfirmacao] = ids };
            
            var jsonPayload = JsonSerializer.Serialize(payload, _jsonOptions);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var response = await _retryPolicy.ExecuteAsync(async () =>
            {
                return await _httpJuridico.PostAsync(endpoint, content, cancellationToken);
            });

            response.EnsureSuccessStatusCode();
            _logger.LogInformation("🟩 Confirmação enviada à Kurier (Jurídico): {Count} publicações", ids.Count);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "❌ Erro de rede ao confirmar publicações");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro ao confirmar publicações");
            throw;
        }
    }

    /// <summary>
    /// Consulta publicações personalizadas já confirmadas (histórico) na Kurier Jurídico
    /// GET /api/KJuridico/ConsultarPublicacoesPersonalizado?data=yyyy-MM-dd&termo=&tribunal=&estado=
    /// </summary>
    public async Task<IReadOnlyList<Publicacao>> ConsultarPublicacoesPersonalizadoAsync(
        DateTime data, 
        string? termo = null, 
        string? tribunal = null, 
        string? estado = null, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("🟣 Consultando publicações personalizadas para {Data:yyyy-MM-dd}...", data);

            var queryParams = new List<string>
            {
                $"data={data:yyyy-MM-dd}"
            };

            if (!string.IsNullOrWhiteSpace(termo))
                queryParams.Add($"termo={Uri.EscapeDataString(termo)}");
            
            if (!string.IsNullOrWhiteSpace(tribunal))
                queryParams.Add($"tribunal={Uri.EscapeDataString(tribunal)}");
            
            if (!string.IsNullOrWhiteSpace(estado))
                queryParams.Add($"estado={Uri.EscapeDataString(estado)}");

            var query = "?" + string.Join("&", queryParams);
            var endpoint = $"api/KJuridico/ConsultarPublicacoesPersonalizado{query}";

            var response = await _retryPolicy.ExecuteAsync(async () =>
            {
                return await _httpJuridico.GetAsync(endpoint, cancellationToken);
            });

            response.EnsureSuccessStatusCode();
            var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
            
            var publicacoes = JsonSerializer.Deserialize<List<Publicacao>>(jsonContent, _jsonOptions) 
                ?? new List<Publicacao>();

            _logger.LogInformation("📜 Encontradas {Count} publicações personalizadas", publicacoes.Count);
            return publicacoes.AsReadOnly();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "❌ Erro de rede ao consultar publicações personalizadas");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro ao consultar publicações personalizadas");
            throw;
        }
    }

    /// <summary>
    /// Libera recursos dos HttpClients
    /// </summary>
    public void Dispose()
    {
        _httpDistribuicao?.Dispose();
        _httpJuridico?.Dispose();
    }
}