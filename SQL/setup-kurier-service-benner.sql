-- =====================================================================
-- SEED: Serviço "KURIER" e parâmetros da integração (PostgreSQL)
-- Executar na BASE do Benner
-- Idempotente: cria/atualiza somente se necessário
-- =====================================================================

DO $$
DECLARE
  v_servico_id BIGINT;
  rec RECORD;
BEGIN
  -- 1) Localiza ou cria o serviço
  SELECT id INTO v_servico_id FROM "INT_Servico" WHERE codigo = 'KURIER';

  IF v_servico_id IS NULL THEN
    INSERT INTO "INT_Servico"(nome, codigo, descricao, tipo, classe, ativo)
    VALUES (
      'Kurier',
      'KURIER',
      'Integração com sistema Kurier (publicações/andamentos)',
      'WebService',
      'Benner.Kurier.Service.KurierIntegration',  -- ajuste se houver classe oficial
      TRUE
    )
    RETURNING id INTO v_servico_id;
    RAISE NOTICE '✅ Serviço KURIER criado com ID: %', v_servico_id;
  ELSE
    UPDATE "INT_Servico"
       SET nome      = 'Kurier',
           descricao = 'Integração com sistema Kurier (publicações/andamentos)',
           tipo      = 'WebService',
           classe    = 'Benner.Kurier.Service.KurierIntegration',
           ativo     = TRUE
     WHERE id = v_servico_id;
    RAISE NOTICE '⚠️  Serviço KURIER já existe com ID: % (atualizado)', v_servico_id;
  END IF;

  -- 2) UPSERT de parâmetros
  -- helper local
  CREATE TEMP TABLE _p(nome TEXT, valor TEXT, sigiloso BOOLEAN) ON COMMIT DROP;

  INSERT INTO _p(nome, valor, sigiloso) VALUES
    ('BaseUrl',           'http://www.kurierservicos.com.br/wsservicos/', FALSE),
    ('UserAgent',         'BennerKurierWorker/1.0',                         FALSE),
    ('TimeoutSeconds',    '100',                                            FALSE),
    ('MaxRetries',        '3',                                              FALSE),

    -- Distribuição (publicações)
    ('LoginDistribuicao', 'o.de.quadro.distribuicao',                       TRUE),
    ('SenhaDistribuicao', '855B07EB-99CE-46F1-81CC-4785B090DD72',           TRUE),

    -- Jurídico (andamentos)
    ('LoginJuridico',     'osvaldoquadro',                                  TRUE),
    ('SenhaJuridico',     '159811',                                         TRUE);

  -- insere/atualiza cada parâmetro
  FOR rec IN SELECT nome, valor, sigiloso FROM _p LOOP
    BEGIN
      -- existe?
      IF EXISTS (SELECT 1 FROM "INT_ParametroServico" s
                 WHERE s.servico_id = v_servico_id AND s.nome = rec.nome) THEN
        UPDATE "INT_ParametroServico" s
           SET valor    = rec.valor,
               sigiloso = rec.sigiloso
         WHERE s.servico_id = v_servico_id
           AND s.nome       = rec.nome;
        RAISE NOTICE '⚠️  Parâmetro % atualizado', rec.nome;
      ELSE
        INSERT INTO "INT_ParametroServico"(servico_id, nome, valor, sigiloso)
        VALUES (v_servico_id, rec.nome, rec.valor, rec.sigiloso);
        RAISE NOTICE '✅ Parâmetro % criado', rec.nome;
      END IF;
    EXCEPTION WHEN others THEN
      -- ignora duplicidade eventual e segue
      RAISE NOTICE '❌ Erro ao processar parâmetro %: %', rec.nome, SQLERRM;
    END;
  END LOOP;
  
  RAISE NOTICE '';
  RAISE NOTICE '🎯 Setup do serviço KURIER concluído com sucesso!';
  RAISE NOTICE '';
END $$;

-- 3) conferência
SELECT 
  '📋 SERVIÇO KURIER CONFIGURADO' AS status;

-- Detalhes do serviço
SELECT 
  id,
  nome,
  codigo,
  descricao,
  tipo,
  classe,
  CASE WHEN ativo THEN '✅ Ativo' ELSE '❌ Inativo' END AS status_ativo
FROM "INT_Servico" 
WHERE codigo = 'KURIER';

-- Parâmetros do serviço (mascarando valores sigilosos)
SELECT 
  nome AS parametro,
  CASE 
    WHEN sigiloso THEN '🔒 [VALOR SIGILOSO]' 
    ELSE valor 
  END AS valor_display,
  CASE WHEN sigiloso THEN '🔐 Sim' ELSE '🔓 Não' END AS sigiloso
FROM "INT_ParametroServico"
WHERE servico_id = (SELECT id FROM "INT_Servico" WHERE codigo = 'KURIER')
ORDER BY nome;


-- ========================================================================
-- INSTRUÇÕES DE EXECUÇÃO:
-- ========================================================================
/*
📝 COMO EXECUTAR ESTE SCRIPT:

1. 🔧 OPÇÃO A - pgAdmin/DBeaver:
   - Conecte no banco do Benner (PostgreSQL)
   - Abra este arquivo: setup-kurier-service-benner.sql  
   - Execute (F5)
   - Veja o SELECT de conferência no final

2. 🔧 OPÇÃO B - psql (linha de comando):
   psql "host=SEU_HOST dbname=SEU_BANCO user=SEU_USUARIO password=SUA_SENHA" -f setup-kurier-service-benner.sql

3. ✅ VERIFICAÇÃO:
   - Deve listar 1 serviço KURIER
   - Deve listar 8 parâmetros criados
   - Valores sigilosos devem aparecer mascarados

4. 🎯 PRÓXIMOS PASSOS:
   - Vá em Administração → Parâmetros de serviços no Benner
   - Pesquise por "Kurier" (ou atualize a página)
   - Se não aparecer, reinicie app pool/IIS do Benner
   - Teste endpoints Kurier no Postman
   - Monitore logs do Railway

5. 🔄 CREDENCIAIS JÁ CONFIGURADAS:
   ✅ LoginDistribuicao: o.de.quadro.distribuicao
   ✅ SenhaDistribuicao: 855B07EB-99CE-46F1-81CC-4785B090DD72  
   ✅ LoginJuridico: osvaldoquadro
   ✅ SenhaJuridico: 159811
   ✅ BaseUrl: https://www.kurierservicos.com.br/wsservicos/
   ✅ UserAgent: BennerKurierWorker/1.0
   ✅ TimeoutSeconds: 100
   ✅ MaxRetries: 3

📋 OBSERVAÇÕES:
- Script é idempotente: pode ser executado múltiplas vezes
- Usa aspas nas tabelas: "INT_Servico", "INT_ParametroServico"  
- Se sua instalação usar minúsculo, remova as aspas
- Credenciais são as mesmas do appsettings.json do projeto
- Valores sigilosos estão marcados como sigiloso=TRUE
*/

-- ========================================================================
-- FIM DO SCRIPT
-- ========================================================================