-- =====================================================================
-- FIX: Resolve problema HTTPS ServicePointManager no serviço Kurier
-- Executar na BASE do Benner para corrigir configurações
-- =====================================================================

DO $$
DECLARE
  v_servico_id BIGINT;
BEGIN
  -- Localizar o serviço KURIER
  SELECT id INTO v_servico_id FROM "INT_Servico" WHERE codigo = 'KURIER';

  IF v_servico_id IS NOT NULL THEN
    -- Atualizar parâmetros para usar HTTP ao invés de HTTPS
    UPDATE "INT_ServicoParametro" 
    SET valor = 'http://www.kurierservicos.com.br/wsservicos/'
    WHERE servico = v_servico_id 
      AND nome = 'BaseUrl'
      AND valor LIKE 'https://%';

    -- Adicionar parâmetro para desabilitar verificação SSL se necessário
    INSERT INTO "INT_ServicoParametro"(servico, nome, valor, sigiloso)
    SELECT v_servico_id, 'DisableSSLVerification', 'true', FALSE
    WHERE NOT EXISTS (
      SELECT 1 FROM "INT_ServicoParametro" 
      WHERE servico = v_servico_id AND nome = 'DisableSSLVerification'
    );

    -- Adicionar timeout estendido para compatibilidade
    INSERT INTO "INT_ServicoParametro"(servico, nome, valor, sigiloso)
    SELECT v_servico_id, 'ConnectionTimeout', '120', FALSE
    WHERE NOT EXISTS (
      SELECT 1 FROM "INT_ServicoParametro" 
      WHERE servico = v_servico_id AND nome = 'ConnectionTimeout'
    );

    -- Forçar HTTP/1.1 para compatibilidade
    INSERT INTO "INT_ServicoParametro"(servico, nome, valor, sigiloso)
    SELECT v_servico_id, 'ForceHttp11', 'true', FALSE
    WHERE NOT EXISTS (
      SELECT 1 FROM "INT_ServicoParametro" 
      WHERE servico = v_servico_id AND nome = 'ForceHttp11'
    );

    RAISE NOTICE '✅ Configurações HTTPS corrigidas para o serviço KURIER (ID: %)', v_servico_id;
    RAISE NOTICE '📋 URLs atualizadas para HTTP para evitar problemas com ServicePointManager';
    RAISE NOTICE '⚙️ Parâmetros de compatibilidade adicionados';
  ELSE
    RAISE NOTICE '❌ Serviço KURIER não encontrado. Execute primeiro o setup-kurier-service-benner.sql';
  END IF;
END $$;

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