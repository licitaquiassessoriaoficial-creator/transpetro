-- =====================================================================
-- SOLUÇÃO COMPLETA: ServicePointManager HTTPS Error
-- Server: 10.28.197.21 | Sistema: PJUR_TR | User: SYSDBA
-- =====================================================================

-- PASSO 1: Remover serviço atual se existir
DELETE FROM INT_ServicoParametro WHERE servico IN (
    SELECT id FROM INT_Servico WHERE codigo = 'KURIER'
);
DELETE FROM INT_Servico WHERE codigo = 'KURIER';

-- PASSO 2: Criar serviço com configurações HTTP (sem HTTPS)
INSERT INTO INT_Servico (nome, codigo, descricao, tipo, classe, ativo)
VALUES (
    'Kurier - Publicacoes Online/Distribuicao', 
    'KURIER',
    'Integracao Kurier (HTTP - compativel ServicePointManager)',
    'WebService',
    'BennerKurierWorker.Service',
    1
);

DECLARE @servico_id BIGINT = SCOPE_IDENTITY();

-- PASSO 3: Inserir parâmetros com URLs HTTP
INSERT INTO INT_ServicoParametro (servico, nome, valor, sigiloso) VALUES
(@servico_id, 'BaseUrl', 'http://www.kurierservicos.com.br/wsservicos/', 0),
(@servico_id, 'LoginDistribuicao', 'o.de.quadro.distribuicao', 1),
(@servico_id, 'SenhaDistribuicao', '855B07EB-99CE-46F1-81CC-4785B090DD72', 1),
(@servico_id, 'LoginJuridico', 'osvaldoquadro', 1), 
(@servico_id, 'SenhaJuridico', '159811', 1),
(@servico_id, 'TimeoutSeconds', '30', 0),
(@servico_id, 'MaxRetries', '2', 0),
(@servico_id, 'UserAgent', 'BennerKurier/1.0', 0),
(@servico_id, 'UseHttpOnly', 'true', 0),
(@servico_id, 'DisableSSL', 'true', 0),
(@servico_id, 'ForceHttp11', 'true', 0);

-- PASSO 4: Verificar se foi criado corretamente
SELECT 
    'Servico criado: ' + s.nome + ' (ID: ' + CAST(s.id AS VARCHAR(10)) + ')' as Status
FROM INT_Servico s 
WHERE s.codigo = 'KURIER';

SELECT 
    p.nome as Parametro,
    p.valor as Valor,
    CASE WHEN p.sigiloso = 1 THEN 'Sim' ELSE 'Nao' END as Sigiloso
FROM INT_Servico s
JOIN INT_ServicoParametro p ON s.id = p.servico
WHERE s.codigo = 'KURIER'
ORDER BY p.nome;

PRINT '✅ Serviço KURIER configurado com HTTP (sem HTTPS)';
PRINT '✅ Compatível com ServicePointManager';
PRINT '✅ Servidor: 10.28.197.21 | PJUR_TR';