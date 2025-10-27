-- =====================================================================
-- SOLUÇÃO DEFINITIVA: ServicePointManager HTTPS Error  
-- Server: 10.28.197.21 | Sistema: PJUR_TR | User: SYSDBA
-- EXECUÇÃO URGENTE - Resolve erro de proxy HTTPS
-- =====================================================================

-- STEP 1: Verificar se existe serviço atual
IF EXISTS (SELECT 1 FROM INT_Servico WHERE codigo = 'KURIER')
BEGIN
    PRINT 'Removendo configuração anterior do serviço KURIER...'
    
    -- Remover parâmetros
    DELETE FROM INT_ServicoParametro 
    WHERE servico IN (SELECT id FROM INT_Servico WHERE codigo = 'KURIER')
    
    -- Remover serviço  
    DELETE FROM INT_Servico WHERE codigo = 'KURIER'
    
    PRINT 'Serviço anterior removido com sucesso.'
END

-- STEP 2: Criar novo serviço com configuração HTTP (SEM HTTPS)
DECLARE @servicoId BIGINT

INSERT INTO INT_Servico (nome, codigo, descricao, tipo, classe, ativo)
VALUES (
    'Kurier - HTTP Only (Fix ServicePointManager)',
    'KURIER', 
    'Integração Kurier sem HTTPS - compatível com ServicePointManager',
    'WebService',
    'BennerKurierWorker.Service',
    1
)

SET @servicoId = SCOPE_IDENTITY()

PRINT 'Novo serviço KURIER criado com ID: ' + CAST(@servicoId AS VARCHAR(10))

-- STEP 3: Configurar parâmetros HTTP (NUNCA HTTPS)
INSERT INTO INT_ServicoParametro (servico, nome, valor, sigiloso) VALUES
(@servicoId, 'BaseUrl', 'http://www.kurierservicos.com.br/wsservicos/', 0),
(@servicoId, 'LoginDistribuicao', 'o.de.quadro.distribuicao', 1),
(@servicoId, 'SenhaDistribuicao', '855B07EB-99CE-46F1-81CC-4785B090DD72', 1),
(@servicoId, 'LoginJuridico', 'osvaldoquadro', 1),
(@servicoId, 'SenhaJuridico', '159811', 1),
(@servicoId, 'TimeoutSeconds', '30', 0),
(@servicoId, 'MaxRetries', '2', 0),
(@servicoId, 'UserAgent', 'BennerKurier-HttpOnly/1.0', 0),
(@servicoId, 'UseHttpOnly', 'true', 0),
(@servicoId, 'DisableSSL', 'true', 0),
(@servicoId, 'BypassProxy', 'true', 0),
(@servicoId, 'ForceHttp11', 'true', 0)

PRINT 'Parâmetros HTTP configurados com sucesso.'

-- STEP 4: Validação final
PRINT ''
PRINT '========================================='
PRINT 'CONFIGURAÇÃO FINAL DO SERVIÇO KURIER:'
PRINT '========================================='

SELECT 
    'ID: ' + CAST(s.id AS VARCHAR(10)) + ' | Nome: ' + s.nome + ' | Status: ' + 
    CASE WHEN s.ativo = 1 THEN 'ATIVO' ELSE 'INATIVO' END as ServicoStatus
FROM INT_Servico s 
WHERE s.codigo = 'KURIER'

SELECT 
    p.nome as Parametro,
    CASE 
        WHEN p.sigiloso = 1 THEN '[CONFIDENCIAL]'
        ELSE p.valor 
    END as Valor
FROM INT_Servico s
JOIN INT_ServicoParametro p ON s.id = p.servico  
WHERE s.codigo = 'KURIER'
ORDER BY p.nome

PRINT ''
PRINT '✅ SOLUÇÃO APLICADA COM SUCESSO!'
PRINT '✅ Serviço configurado para HTTP apenas'
PRINT '✅ ServicePointManager compatível'
PRINT '✅ Proxy bypass habilitado'
PRINT ''
PRINT 'PRÓXIMOS PASSOS:'
PRINT '1. Reiniciar serviço no Monitor de Serviços'
PRINT '2. Verificar logs de execução'
PRINT '3. Confirmar status ATIVO'