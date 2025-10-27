namespace BennerKurierWorker.Infrastructure;

/// <summary>
/// Configurações para conexão com a API Kurier - Distribuição
/// </summary>
public class KurierSettings
{
    /// <summary>
    /// URL base da API Kurier
    /// </summary>
    public string BaseUrl { get; set; } = "http://www.kurierservicos.com.br/wsservicos/";
    
    /// <summary>
    /// Usuário para autenticação Basic Auth
    /// </summary>
    public string Usuario { get; set; } = string.Empty;
    
    /// <summary>
    /// Senha para autenticação Basic Auth
    /// </summary>
    public string Senha { get; set; } = string.Empty;
    
    /// <summary>
    /// Timeout em segundos para requisições HTTP
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Número máximo de tentativas para retry policy
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Delay inicial em segundos para retry policy
    /// </summary>
    public int DelayInicial { get; set; } = 1;

    /// <summary>
    /// Chave para confirmar publicações: "Identificador" ou "NumeroProcesso"
    /// </summary>
    public string? ConfirmarPublicacoesKey { get; set; } = "Identificador";

    /// <summary>
    /// User-Agent para requisições HTTP
    /// </summary>
    public string UserAgent { get; set; } = "BennerKurierWorker/1.0 (Railway)";
}

/// <summary>
/// Configurações para conexão com a API Kurier - Jurídico (Publicações)
/// </summary>
public class KurierJuridicoSettings
{
    /// <summary>
    /// URL base da API Kurier Jurídico
    /// </summary>
    public string BaseUrl { get; set; } = "https://www.kurierservicos.com.br/wsservicos/";
    
    /// <summary>
    /// Usuário para autenticação Basic Auth
    /// </summary>
    public string Usuario { get; set; } = string.Empty;
    
    /// <summary>
    /// Senha para autenticação Basic Auth
    /// </summary>
    public string Senha { get; set; } = string.Empty;
    
    /// <summary>
    /// Timeout em segundos para requisições HTTP
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Número máximo de tentativas para retry policy
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Delay inicial em segundos para retry policy
    /// </summary>
    public int DelayInicial { get; set; } = 1;

    /// <summary>
    /// Chave para confirmar publicações: "Identificador" ou "NumeroProcesso"
    /// </summary>
    public string? ConfirmarPublicacoesKey { get; set; } = "Identificador";

    /// <summary>
    /// User-Agent para requisições HTTP
    /// </summary>
    public string UserAgent { get; set; } = "BennerKurierWorker/1.0 (Railway)";
}