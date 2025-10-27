-- ========================================================================
-- SCRIPT SQL IDEMPOTENTE - CRIAÇÃO DO SERVIÇO KURIER NO BENNER
-- ========================================================================
-- Descrição: Cria o serviço KURIER na base do Benner com parâmetros padrão
-- Banco: PostgreSQL
-- Versão: 1.0
-- Data: 2025-10-27
-- ========================================================================

BEGIN;

-- ========================================================================
-- 1. CRIAÇÃO DO SERVIÇO KURIER (se não existir)
-- ========================================================================

DO $$
DECLARE
    v_servico_id INTEGER;
    v_count INTEGER;
BEGIN
    -- Verifica se o serviço KURIER já existe
    SELECT COUNT(*) INTO v_count 
    FROM INT_Servico 
    WHERE codigo = 'KURIER';
    
    -- Se não existir, cria o serviço
    IF v_count = 0 THEN
        -- Obtém o próximo ID para o serviço
        SELECT COALESCE(MAX(id), 0) + 1 INTO v_servico_id 
        FROM INT_Servico;
        
        -- Insere o serviço KURIER
        INSERT INTO INT_Servico (
            id,
            nome,
            codigo,
            descricao,
            tipo,
            classe,
            ativo
        ) VALUES (
            v_servico_id,
            'Kurier',
            'KURIER',
            'Integração com sistema Kurier (publicações/andamentos)',
            'WebService',
            'Benner.Kurier.Service.KurierIntegration',
            true
        );
        
        RAISE NOTICE '✅ Serviço KURIER criado com ID: %', v_servico_id;
    ELSE
        -- Obtém o ID do serviço existente
        SELECT id INTO v_servico_id 
        FROM INT_Servico 
        WHERE codigo = 'KURIER';
        
        RAISE NOTICE '⚠️  Serviço KURIER já existe com ID: %', v_servico_id;
    END IF;
    
    -- ========================================================================
    -- 2. CRIAÇÃO DOS PARÂMETROS DO SERVIÇO (se não existirem)
    -- ========================================================================
    
    -- Parâmetro: BaseUrl
    IF NOT EXISTS (
        SELECT 1 FROM INT_ParametroServico 
        WHERE servico_id = v_servico_id AND nome = 'BaseUrl'
    ) THEN
        INSERT INTO INT_ParametroServico (
            id,
            servico_id,
            nome,
            valor,
            sigiloso
        ) VALUES (
            (SELECT COALESCE(MAX(id), 0) + 1 FROM INT_ParametroServico),
            v_servico_id,
            'BaseUrl',
            'https://www.kurierservicos.com.br/wsservicos/',
            false
        );
        RAISE NOTICE '✅ Parâmetro BaseUrl criado';
    ELSE
        RAISE NOTICE '⚠️  Parâmetro BaseUrl já existe';
    END IF;
    
    -- Parâmetro: UserAgent
    IF NOT EXISTS (
        SELECT 1 FROM INT_ParametroServico 
        WHERE servico_id = v_servico_id AND nome = 'UserAgent'
    ) THEN
        INSERT INTO INT_ParametroServico (
            id,
            servico_id,
            nome,
            valor,
            sigiloso
        ) VALUES (
            (SELECT COALESCE(MAX(id), 0) + 1 FROM INT_ParametroServico),
            v_servico_id,
            'UserAgent',
            'BennerKurierWorker/1.0',
            false
        );
        RAISE NOTICE '✅ Parâmetro UserAgent criado';
    ELSE
        RAISE NOTICE '⚠️  Parâmetro UserAgent já existe';
    END IF;
    
    -- Parâmetro: LoginDistribuicao (sigiloso) - Kurier Distribuição
    IF NOT EXISTS (
        SELECT 1 FROM INT_ParametroServico 
        WHERE servico_id = v_servico_id AND nome = 'LoginDistribuicao'
    ) THEN
        INSERT INTO INT_ParametroServico (
            id,
            servico_id,
            nome,
            valor,
            sigiloso
        ) VALUES (
            (SELECT COALESCE(MAX(id), 0) + 1 FROM INT_ParametroServico),
            v_servico_id,
            'LoginDistribuicao',
            'o.de.quadro.distribuicao',
            true
        );
        RAISE NOTICE '✅ Parâmetro LoginDistribuicao criado (sigiloso)';
    ELSE
        RAISE NOTICE '⚠️  Parâmetro LoginDistribuicao já existe';
    END IF;
    
    -- Parâmetro: SenhaDistribuicao (sigiloso) - Kurier Distribuição
    IF NOT EXISTS (
        SELECT 1 FROM INT_ParametroServico 
        WHERE servico_id = v_servico_id AND nome = 'SenhaDistribuicao'
    ) THEN
        INSERT INTO INT_ParametroServico (
            id,
            servico_id,
            nome,
            valor,
            sigiloso
        ) VALUES (
            (SELECT COALESCE(MAX(id), 0) + 1 FROM INT_ParametroServico),
            v_servico_id,
            'SenhaDistribuicao',
            '855B07EB-99CE-46F1-81CC-4785B090DD72',
            true
        );
        RAISE NOTICE '✅ Parâmetro SenhaDistribuicao criado (sigiloso)';
    ELSE
        RAISE NOTICE '⚠️  Parâmetro SenhaDistribuicao já existe';
    END IF;
    
    -- Parâmetro: LoginJuridico (sigiloso) - Kurier Jurídico/Publicações
    IF NOT EXISTS (
        SELECT 1 FROM INT_ParametroServico 
        WHERE servico_id = v_servico_id AND nome = 'LoginJuridico'
    ) THEN
        INSERT INTO INT_ParametroServico (
            id,
            servico_id,
            nome,
            valor,
            sigiloso
        ) VALUES (
            (SELECT COALESCE(MAX(id), 0) + 1 FROM INT_ParametroServico),
            v_servico_id,
            'LoginJuridico',
            'osvaldoquadro',
            true
        );
        RAISE NOTICE '✅ Parâmetro LoginJuridico criado (sigiloso)';
    ELSE
        RAISE NOTICE '⚠️  Parâmetro LoginJuridico já existe';
    END IF;
    
    -- Parâmetro: SenhaJuridico (sigiloso) - Kurier Jurídico/Publicações
    IF NOT EXISTS (
        SELECT 1 FROM INT_ParametroServico 
        WHERE servico_id = v_servico_id AND nome = 'SenhaJuridico'
    ) THEN
        INSERT INTO INT_ParametroServico (
            id,
            servico_id,
            nome,
            valor,
            sigiloso
        ) VALUES (
            (SELECT COALESCE(MAX(id), 0) + 1 FROM INT_ParametroServico),
            v_servico_id,
            'SenhaJuridico',
            '159811',
            true
        );
        RAISE NOTICE '✅ Parâmetro SenhaJuridico criado (sigiloso)';
    ELSE
        RAISE NOTICE '⚠️  Parâmetro SenhaJuridico já existe';
    END IF;
    
    -- Parâmetro: TimeoutSeconds - Timeout para requisições
    IF NOT EXISTS (
        SELECT 1 FROM INT_ParametroServico 
        WHERE servico_id = v_servico_id AND nome = 'TimeoutSeconds'
    ) THEN
        INSERT INTO INT_ParametroServico (
            id,
            servico_id,
            nome,
            valor,
            sigiloso
        ) VALUES (
            (SELECT COALESCE(MAX(id), 0) + 1 FROM INT_ParametroServico),
            v_servico_id,
            'TimeoutSeconds',
            '100',
            false
        );
        RAISE NOTICE '✅ Parâmetro TimeoutSeconds criado';
    ELSE
        RAISE NOTICE '⚠️  Parâmetro TimeoutSeconds já existe';
    END IF;
    
    -- Parâmetro: MaxRetries - Número máximo de tentativas
    IF NOT EXISTS (
        SELECT 1 FROM INT_ParametroServico 
        WHERE servico_id = v_servico_id AND nome = 'MaxRetries'
    ) THEN
        INSERT INTO INT_ParametroServico (
            id,
            servico_id,
            nome,
            valor,
            sigiloso
        ) VALUES (
            (SELECT COALESCE(MAX(id), 0) + 1 FROM INT_ParametroServico),
            v_servico_id,
            'MaxRetries',
            '3',
            false
        );
        RAISE NOTICE '✅ Parâmetro MaxRetries criado';
    ELSE
        RAISE NOTICE '⚠️  Parâmetro MaxRetries já existe';
    END IF;
    
    RAISE NOTICE '';
    RAISE NOTICE '🎯 Setup do serviço KURIER concluído com sucesso!';
    RAISE NOTICE '';
    
END $$;

-- ========================================================================
-- 3. COMMIT DA TRANSAÇÃO
-- ========================================================================

COMMIT;

-- ========================================================================
-- 4. CONSULTA FINAL - LISTAGEM DO SERVIÇO E PARÂMETROS CRIADOS
-- ========================================================================

SELECT 
    '📋 SERVIÇO KURIER CONFIGURADO' AS status;

-- Detalhes do serviço
SELECT 
    s.id,
    s.nome,
    s.codigo,
    s.descricao,
    s.tipo,
    s.classe,
    CASE WHEN s.ativo THEN '✅ Ativo' ELSE '❌ Inativo' END AS status_ativo
FROM INT_Servico s
WHERE s.codigo = 'KURIER';

-- Parâmetros do serviço (mascarando valores sigilosos)
SELECT 
    p.id,
    p.nome AS parametro,
    CASE 
        WHEN p.sigiloso THEN '🔒 [VALOR SIGILOSO]' 
        ELSE p.valor 
    END AS valor_display,
    CASE WHEN p.sigiloso THEN '🔐 Sim' ELSE '🔓 Não' END AS sigiloso
FROM INT_ParametroServico p
INNER JOIN INT_Servico s ON s.id = p.servico_id
WHERE s.codigo = 'KURIER'
ORDER BY p.nome;

-- ========================================================================
-- INSTRUÇÕES PÓS-EXECUÇÃO:
-- ========================================================================
/*
📝 PRÓXIMOS PASSOS APÓS EXECUTAR ESTE SCRIPT:

1. ✅ CREDENCIAIS JÁ CONFIGURADAS:
   - LoginDistribuicao: 'o.de.quadro.distribuicao' ✅
   - SenhaDistribuicao: '855B07EB-99CE-46F1-81CC-4785B090DD72' ✅
   - LoginJuridico: 'osvaldoquadro' ✅  
   - SenhaJuridico: '159811' ✅
   - BaseUrl: 'https://www.kurierservicos.com.br/wsservicos/' ✅
   - UserAgent: 'BennerKurierWorker/1.0' ✅

2. 🔧 VALIDAR CONFIGURAÇÃO:
   - Testar conectividade com a API do Kurier
   - Verificar se as credenciais estão funcionando
   - Executar um teste de integração com BennerKurierWorker

3. 📊 MONITORAMENTO:
   - Verificar logs na tabela INT_LogServico (se implementada)
   - Acompanhar execuções do BennerKurierWorker
   - Monitorar performance das integrações
   - Verificar sincronização entre appsettings.json e parâmetros do Benner

4. 🛡️ SEGURANÇA:
   - Garantir que apenas usuários autorizados tenham acesso aos parâmetros sigilosos
   - Implementar rotação periódica de credenciais se necessário
   - Monitorar tentativas de acesso não autorizadas
   - Considerar uso de Azure Key Vault ou similar para credenciais em produção

5. 🔄 SINCRONIZAÇÃO:
   - As credenciais neste script estão sincronizadas com appsettings.json
   - Em caso de mudança de credenciais, atualizar ambos os locais
   - O BennerKurierWorker pode usar tanto appsettings quanto parâmetros do Benner
*/

-- ========================================================================
-- FIM DO SCRIPT
-- ========================================================================