using BennerKurierWorker.Domain;

namespace BennerKurierWorker.Infrastructure;

/// <summary>
/// Interface para comunicação com a API oficial da Kurier
/// Base URL: https://www.kurierservicos.com.br/wsservicos/
/// Documentação: KDistribuicao (03/2024) e KJuridico (v.01.2019)
/// </summary>
public interface IKurierClient
{
    #region Conexão e Testes

    /// <summary>
    /// Testa a conexão com a API Kurier em produção
    /// </summary>
    Task<bool> TestarConexaoKurierAsync(CancellationToken cancellationToken = default);

    #endregion

    #region KDistribuicao (Distribuições)

    /// <summary>
    /// Consulta quantidade de distribuições disponíveis para consumo
    /// GET /api/KDistribuicao/ConsultarQuantidadeDistribuicoesDisponiveis
    /// </summary>
    Task<int> ConsultarQuantidadeDistribuicoesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Consulta novas distribuições pendentes (sem filtro)
    /// GET /api/KDistribuicao/ConsultarDistribuicoes
    /// </summary>
    Task<IReadOnlyList<Distribuicao>> ConsultarDistribuicoesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirma leitura de distribuições
    /// POST /api/KDistribuicao/ConfirmarDistribuicoes
    /// Payload: { "NumeroProcesso": [ "0000000-00.0000.0.00.0000", ... ] }
    /// </summary>
    Task ConfirmarDistribuicoesAsync(IEnumerable<string> numerosProcesso, CancellationToken cancellationToken = default);

    /// <summary>
    /// Consulta distribuições já confirmadas por período
    /// GET /api/KDistribuicao/ConsultarDistribuicoesConfirmadas?tipoFiltro={DATA_CONSUMO|DATA_DISTRIBUICAO}&dataInicial=yyyy-MM-dd&dataFinal=yyyy-MM-dd
    /// </summary>
    Task<IReadOnlyList<Distribuicao>> ConsultarDistribuicoesConfirmadasAsync(
        string tipoFiltro, 
        DateTime dataInicial, 
        DateTime dataFinal, 
        CancellationToken cancellationToken = default);

    #endregion

    #region KJuridico (Publicações)

    /// <summary>
    /// Consulta quantidade de publicações disponíveis
    /// GET /api/KJuridico/ConsultarQuantidadePublicacoesDisponiveis
    /// </summary>
    Task<int> ConsultarQuantidadePublicacoesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Consulta publicações pendentes (até 50 por requisição)
    /// GET /api/KJuridico/ConsultarPublicacoes
    /// </summary>
    /// <param name="somenteResumos">Se true, busca apenas resumos; se false, inteiro teor</param>
    Task<IReadOnlyList<Publicacao>> ConsultarPublicacoesAsync(bool somenteResumos = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirma leitura de publicações
    /// POST /api/KJuridico/ConfirmarPublicacoes
    /// TODO: A chave pode ser "Identificador" ou "NumeroProcesso" conforme conta/contrato
    /// Configurar via Monitoring:ConfirmarPublicacoesKey
    /// </summary>
    Task ConfirmarPublicacoesAsync(IEnumerable<string> idsOuNumerosProcesso, CancellationToken cancellationToken = default);

    /// <summary>
    /// Consulta publicações personalizadas já confirmadas (histórico)
    /// GET /api/KJuridico/ConsultarPublicacoesPersonalizado?data=yyyy-MM-dd&termo=&tribunal=&estado=
    /// </summary>
    Task<IReadOnlyList<Publicacao>> ConsultarPublicacoesPersonalizadoAsync(
        DateTime data, 
        string? termo = null, 
        string? tribunal = null, 
        string? estado = null, 
        CancellationToken cancellationToken = default);

    #endregion
}