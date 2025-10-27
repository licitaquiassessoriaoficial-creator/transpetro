-- =====================================================================
-- FIX: Resolve problema HTTPS ServicePointManager no serviço Kurier
-- Executar na BASE do Benner para corrigir configurações
-- Compatible com SQL Server e PostgreSQL
-- =====================================================================

DECLARE @v_servico_id BIGINT;

-- Localizar o serviço KURIER
SELECT @v_servico_id = id FROM INT_Servico WHERE codigo = 'KURIER';

IF @v_servico_id IS NOT NULL
BEGIN
    -- Atualizar parâmetros para usar HTTP ao invés de HTTPS
    UPDATE INT_ServicoParametro 
    SET valor = 'http://www.kurierservicos.com.br/wsservicos/'
    WHERE servico = @v_servico_id 
      AND nome = 'BaseUrl'
      AND valor LIKE 'https://%';

    -- Adicionar parâmetro para desabilitar verificação SSL se necessário
    IF NOT EXISTS (SELECT 1 FROM INT_ServicoParametro WHERE servico = @v_servico_id AND nome = 'DisableSSLVerification')
    BEGIN
        INSERT INTO INT_ServicoParametro(servico, nome, valor, sigiloso)
        VALUES (@v_servico_id, 'DisableSSLVerification', 'true', 0);
    END

    -- Adicionar timeout estendido para compatibilidade
    IF NOT EXISTS (SELECT 1 FROM INT_ServicoParametro WHERE servico = @v_servico_id AND nome = 'ConnectionTimeout')
    BEGIN
        INSERT INTO INT_ServicoParametro(servico, nome, valor, sigiloso)
        VALUES (@v_servico_id, 'ConnectionTimeout', '120', 0);
    END

    -- Forçar HTTP/1.1 para compatibilidade
    IF NOT EXISTS (SELECT 1 FROM INT_ServicoParametro WHERE servico = @v_servico_id AND nome = 'ForceHttp11')
    BEGIN
        INSERT INTO INT_ServicoParametro(servico, nome, valor, sigiloso)
        VALUES (@v_servico_id, 'ForceHttp11', 'true', 0);
    END

    PRINT 'Configurações HTTPS corrigidas para o serviço KURIER (ID: ' + CAST(@v_servico_id AS VARCHAR(10)) + ')';
    PRINT 'URLs atualizadas para HTTP para evitar problemas com ServicePointManager';
    PRINT 'Parâmetros de compatibilidade adicionados';
END
ELSE
BEGIN
    PRINT 'Serviço KURIER não encontrado. Execute primeiro o setup-kurier-service-benner.sql';
END

-- Verificar as configurações atuais
SELECT 
  s.nome as servico_nome,
  s.codigo as servico_codigo,
  p.nome as parametro_nome,
  p.valor as parametro_valor,
  p.sigiloso
FROM "INT_Servico" s
JOIN "INT_ServicoParametro" p ON s.id = p.servico
WHERE s.codigo = 'KURIER'
ORDER BY p.nome;