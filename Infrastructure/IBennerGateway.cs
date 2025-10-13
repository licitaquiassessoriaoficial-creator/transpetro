using BennerKurierWorker.Domain;

namespace BennerKurierWorker.Infrastructure;

/// <summary>
/// Interface para operações de persistência no sistema Benner
/// </summary>
public interface IBennerGateway
{
    /// <summary>
    /// Salva uma lista de distribuições no banco de dados e retorna sucesso da operação
    /// </summary>
    /// <param name="distribuicoes">Lista de distribuições</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>True se todas foram salvas com sucesso, false em caso de erro</returns>
    Task<bool> SalvarDistribuicoesAsync(
        IEnumerable<Distribuicao> distribuicoes, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Salva uma lista de publicações no banco de dados e retorna sucesso da operação
    /// </summary>
    /// <param name="publicacoes">Lista de publicações</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>True se todas foram salvas com sucesso, false em caso de erro</returns>
    Task<bool> SalvarPublicacoesAsync(
        IEnumerable<Publicacao> publicacoes, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém distribuições não confirmadas para envio de confirmação
    /// </summary>
    /// <param name="limite">Número máximo de registros</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Lista de distribuições não confirmadas</returns>
    Task<IEnumerable<Distribuicao>> ObterDistribuicoesNaoConfirmadasAsync(
        int limite = 100, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém publicações não confirmadas para envio de confirmação
    /// </summary>
    /// <param name="limite">Número máximo de registros</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Lista de publicações não confirmadas</returns>
    Task<IEnumerable<Publicacao>> ObterPublicacoesNaoConfirmadasAsync(
        int limite = 100, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marca distribuições como confirmadas
    /// </summary>
    /// <param name="ids">IDs das distribuições</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Número de registros atualizados</returns>
    Task<int> MarcarDistribuicoesComoConfirmadasAsync(
        IEnumerable<string> ids, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marca publicações como confirmadas
    /// </summary>
    /// <param name="ids">IDs das publicações</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Número de registros atualizados</returns>
    Task<int> MarcarPublicacoesComoConfirmadasAsync(
        IEnumerable<string> ids, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica se a conexão com o banco está funcionando
    /// </summary>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>True se a conexão está OK</returns>
    Task<bool> TestarConexaoAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Salva um relatório de monitoramento no banco de dados
    /// </summary>
    /// <param name="relatorio">Relatório de monitoramento</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>True se salvou com sucesso</returns>
    Task<bool> SalvarRelatorioMonitoramentoAsync(
        MonitoramentoKurier relatorio, 
        CancellationToken cancellationToken = default);
}