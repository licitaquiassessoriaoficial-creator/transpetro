using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using BennerKurierWorker.Domain;

namespace BennerKurierWorker.Infrastructure;

/// <summary>
/// Cliente para comunicação com a API oficial da Kurier
/// Implementação para Railway deployment com retry policies
/// Base URL: https://www.kurierservicos.com.br/wsservicos/
/// </summary>
public class KurierClient : IKurierClient, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly KurierSettings _settings;
    private readonly ILogger<KurierClient> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public KurierClient(
        HttpClient httpClient,
        IOptions<KurierSettings> settings,
        ILogger<KurierClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        ConfigurarHttpClient();
    }

    private void ConfigurarHttpClient()
    {
        // Base URL: https://www.kurierservicos.com.br/wsservicos/
        _httpClient.BaseAddress = new Uri(_settings.BaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds);

        // Basic Authentication
        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{_settings.Usuario}:{_settings.Senha}"));
        _httpClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Basic", credentials);

        // Headers padrão
        _httpClient.DefaultRequestHeaders.Accept.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.Add("User-Agent", 
            "BennerKurierWorker/1.0 (.NET 8.0; Railway)");
    }

    #region KDistribuicao (Distribuições)

    /// <summary>
    /// Consulta quantidade de distribuições disponíveis para consumo
    /// GET /api/KDistribuicao/ConsultarQuantidadeDistribuicoesDisponiveis
    /// </summary>
    public async Task<int> ConsultarQuantidadeDistribuicoesAsync(CancellationToken cancellationToken = default)
    {
        const string endpoint = "api/KDistribuicao/ConsultarQuantidadeDistribuicoesDisponiveis";
        
        try
        {
            _logger.LogInformation("Consultando quantidade de distribuições disponíveis...");
            
            var response = await ExecutarComRetryAsync(
                () => _httpClient.GetAsync(endpoint, cancellationToken),
                endpoint,
                cancellationToken);

            var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
            
            if (int.TryParse(jsonContent.Trim('"'), out var quantidade))
            {
                _logger.LogInformation("Quantidade de distribuições: {Quantidade}", quantidade);
                return quantidade;
            }

            _logger.LogWarning("Resposta inválida para quantidade de distribuições: {Content}", jsonContent);
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao consultar quantidade de distribuições");
            throw;
        }
    }

    /// <summary>
    /// Consulta novas distribuições pendentes (sem filtro)
    /// GET /api/KDistribuicao/ConsultarDistribuicoes
    /// </summary>
    public async Task<IReadOnlyList<Distribuicao>> ConsultarDistribuicoesAsync(CancellationToken cancellationToken = default)
    {
        const string endpoint = "api/KDistribuicao/ConsultarDistribuicoes";
        
        try
        {
            _logger.LogInformation("Consultando distribuições pendentes...");
            
            var response = await ExecutarComRetryAsync(
                () => _httpClient.GetAsync(endpoint, cancellationToken),
                endpoint,
                cancellationToken);

            var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
            
            var distribuicoes = JsonSerializer.Deserialize<List<Distribuicao>>(jsonContent, _jsonOptions) 
                ?? new List<Distribuicao>();

            _logger.LogInformation("Encontradas {Count} distribuições pendentes", distribuicoes.Count);
            return distribuicoes.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao consultar distribuições pendentes");
            throw;
        }
    }

    /// <summary>
    /// Confirma leitura de distribuições
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

            _logger.LogInformation("Confirmando {Count} distribuições...", numeros.Count);

            var payload = new { NumeroProcesso = numeros };
            var jsonPayload = JsonSerializer.Serialize(payload, _jsonOptions);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var response = await ExecutarComRetryAsync(
                () => _httpClient.PostAsync(endpoint, content, cancellationToken),
                endpoint,
                cancellationToken);

            _logger.LogInformation("Distribuições confirmadas com sucesso: {Count}", numeros.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao confirmar distribuições");
            throw;
        }
    }

    /// <summary>
    /// Consulta distribuições já confirmadas por período
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
            _logger.LogInformation("Consultando distribuições confirmadas: {TipoFiltro} de {DataInicial:yyyy-MM-dd} a {DataFinal:yyyy-MM-dd}", 
                tipoFiltro, dataInicial, dataFinal);

            var query = $"?tipoFiltro={tipoFiltro}&dataInicial={dataInicial:yyyy-MM-dd}&dataFinal={dataFinal:yyyy-MM-dd}";
            var endpoint = $"api/KDistribuicao/ConsultarDistribuicoesConfirmadas{query}";

            var response = await ExecutarComRetryAsync(
                () => _httpClient.GetAsync(endpoint, cancellationToken),
                endpoint,
                cancellationToken);

            var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
            
            var distribuicoes = JsonSerializer.Deserialize<List<Distribuicao>>(jsonContent, _jsonOptions) 
                ?? new List<Distribuicao>();

            _logger.LogInformation("Encontradas {Count} distribuições confirmadas", distribuicoes.Count);
            return distribuicoes.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao consultar distribuições confirmadas");
            throw;
        }
    }

    #endregion

    #region KJuridico (Publicações)

    /// <summary>
    /// Consulta quantidade de publicações disponíveis
    /// GET /api/KJuridico/ConsultarQuantidadePublicacoesDisponiveis
    /// </summary>
    public async Task<int> ConsultarQuantidadePublicacoesAsync(CancellationToken cancellationToken = default)
    {
        const string endpoint = "api/KJuridico/ConsultarQuantidadePublicacoesDisponiveis";
        
        try
        {
            _logger.LogInformation("Consultando quantidade de publicações disponíveis...");
            
            var response = await ExecutarComRetryAsync(
                () => _httpClient.GetAsync(endpoint, cancellationToken),
                endpoint,
                cancellationToken);

            var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
            
            if (int.TryParse(jsonContent.Trim('"'), out var quantidade))
            {
                _logger.LogInformation("Quantidade de publicações: {Quantidade}", quantidade);
                return quantidade;
            }

            _logger.LogWarning("Resposta inválida para quantidade de publicações: {Content}", jsonContent);
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao consultar quantidade de publicações");
            throw;
        }
    }

    /// <summary>
    /// Consulta publicações pendentes (até 50 por requisição)
    /// GET /api/KJuridico/ConsultarPublicacoes
    /// </summary>
    /// <param name="somenteResumos">Se true, busca apenas resumos; se false, inteiro teor</param>
    public async Task<IReadOnlyList<Publicacao>> ConsultarPublicacoesAsync(bool somenteResumos = true, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Consultando publicações pendentes (somenteResumos: {SomenteResumos})...", somenteResumos);
            
            var query = somenteResumos ? "?somenteResumos=true" : "";
            var endpoint = $"api/KJuridico/ConsultarPublicacoes{query}";

            var response = await ExecutarComRetryAsync(
                () => _httpClient.GetAsync(endpoint, cancellationToken),
                endpoint,
                cancellationToken);

            var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
            
            var publicacoes = JsonSerializer.Deserialize<List<Publicacao>>(jsonContent, _jsonOptions) 
                ?? new List<Publicacao>();

            _logger.LogInformation("Encontradas {Count} publicações pendentes", publicacoes.Count);
            return publicacoes.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao consultar publicações pendentes");
            throw;
        }
    }

    /// <summary>
    /// Confirma leitura de publicações
    /// POST /api/KJuridico/ConfirmarPublicacoes
    /// TODO: A chave pode ser "Identificador" ou "NumeroProcesso" conforme conta/contrato
    /// Configurar via Monitoring:ConfirmarPublicacoesKey
    /// </summary>
    public async Task ConfirmarPublicacoesAsync(IEnumerable<string> idsOuNumerosProcesso, CancellationToken cancellationToken = default)
    {
        const string endpoint = "api/KJuridico/ConfirmarPublicacoes";
        
        try
        {
            var ids = idsOuNumerosProcesso.ToList();
            
            if (!ids.Any())
            {
                _logger.LogInformation("Nenhum ID para confirmar publicações");
                return;
            }

            _logger.LogInformation("Confirmando {Count} publicações...", ids.Count);

            // TODO: Configurar via settings qual chave usar: "Identificador" ou "NumeroProcesso"
            var chaveConfirmacao = _settings.ConfirmarPublicacoesKey ?? "Identificador";
            var payload = new Dictionary<string, object> { [chaveConfirmacao] = ids };
            
            var jsonPayload = JsonSerializer.Serialize(payload, _jsonOptions);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var response = await ExecutarComRetryAsync(
                () => _httpClient.PostAsync(endpoint, content, cancellationToken),
                endpoint,
                cancellationToken);

            _logger.LogInformation("Publicações confirmadas com sucesso: {Count}", ids.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao confirmar publicações");
            throw;
        }
    }

    /// <summary>
    /// Consulta publicações personalizadas já confirmadas (histórico)
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
            _logger.LogInformation("Consultando publicações personalizadas para {Data:yyyy-MM-dd}...", data);

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

            var response = await ExecutarComRetryAsync(
                () => _httpClient.GetAsync(endpoint, cancellationToken),
                endpoint,
                cancellationToken);

            var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
            
            var publicacoes = JsonSerializer.Deserialize<List<Publicacao>>(jsonContent, _jsonOptions) 
                ?? new List<Publicacao>();

            _logger.LogInformation("Encontradas {Count} publicações personalizadas", publicacoes.Count);
            return publicacoes.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao consultar publicações personalizadas");
            throw;
        }
    }

    #endregion

    #region Retry Policy

    /// <summary>
    /// Executa requisição HTTP com política de retry exponential backoff
    /// </summary>
    private async Task<HttpResponseMessage> ExecutarComRetryAsync(
        Func<Task<HttpResponseMessage>> operation,
        string endpoint,
        CancellationToken cancellationToken)
    {
        var maxTentativas = _settings.MaxRetries;
        var delayInicial = TimeSpan.FromSeconds(_settings.DelayInicial);

        for (int tentativa = 1; tentativa <= maxTentativas; tentativa++)
        {
            try
            {
                var response = await operation();
                
                if (response.IsSuccessStatusCode)
                {
                    return response;
                }

                // Se não for sucesso, mas é última tentativa, lançar exceção
                if (tentativa == maxTentativas)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    throw new HttpRequestException(
                        $"Falha na requisição {endpoint} após {maxTentativas} tentativas. " +
                        $"Status: {response.StatusCode}, Conteúdo: {errorContent}");
                }

                _logger.LogWarning("Tentativa {Tentativa}/{MaxTentativas} falhou para {Endpoint}. Status: {StatusCode}",
                    tentativa, maxTentativas, endpoint, response.StatusCode);
            }
            catch (Exception ex) when (tentativa < maxTentativas && 
                (ex is HttpRequestException || ex is TaskCanceledException))
            {
                _logger.LogWarning(ex, "Tentativa {Tentativa}/{MaxTentativas} falhou para {Endpoint}",
                    tentativa, maxTentativas, endpoint);
            }

            // Exponential backoff: 1s, 2s, 4s, 8s...
            var delay = TimeSpan.FromMilliseconds(delayInicial.TotalMilliseconds * Math.Pow(2, tentativa - 1));
            await Task.Delay(delay, cancellationToken);
        }

        throw new InvalidOperationException("Este ponto não deveria ser alcançado");
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        _httpClient?.Dispose();
    }

    #endregion
}