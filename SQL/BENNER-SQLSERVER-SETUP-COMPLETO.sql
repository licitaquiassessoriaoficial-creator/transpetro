-- =====================================================================
-- SETUP COMPLETO BENNER SQL SERVER - INTEGRAÇÃO KURIER
-- Servidor: 10.28.197.21 | Database: BENNER_PRODUCAO
-- Execute este script como SYSDBA ou administrador
-- =====================================================================

USE BENNER_PRODUCAO
GO

PRINT '🔧 INICIANDO CONFIGURAÇÃO COMPLETA KURIER NO BENNER...'
PRINT '======================================================'

-- STEP 1: Remover configurações anteriores (se existirem)
PRINT ''
PRINT '🧹 Limpando configurações anteriores...'

IF EXISTS (SELECT 1 FROM INT_Servico WHERE codigo = 'KURIER')
BEGIN
    -- Remover parâmetros
    DELETE FROM INT_ServicoParametro 
    WHERE servico IN (SELECT id FROM INT_Servico WHERE codigo = 'KURIER')
    
    -- Remover serviço  
    DELETE FROM INT_Servico WHERE codigo = 'KURIER'
    
    PRINT '✅ Configuração anterior removida'
END
ELSE
BEGIN
    PRINT '✅ Nenhuma configuração anterior encontrada'
END

-- STEP 2: Criar usuário para integração (se não existir)
PRINT ''
PRINT '👤 Criando usuário de integração...'

IF NOT EXISTS (SELECT 1 FROM sys.sql_logins WHERE name = 'kurier_user')
BEGIN
    CREATE LOGIN kurier_user WITH PASSWORD = 'kurier_pass@2025!', 
        DEFAULT_DATABASE = BENNER_PRODUCAO,
        CHECK_EXPIRATION = OFF,
        CHECK_POLICY = OFF
    PRINT '✅ Login kurier_user criado'
END
ELSE
BEGIN
    PRINT '✅ Login kurier_user já existe'
END

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = 'kurier_user')
BEGIN
    CREATE USER kurier_user FOR LOGIN kurier_user
    PRINT '✅ User kurier_user criado no database'
END
ELSE
BEGIN
    PRINT '✅ User kurier_user já existe no database'
END

-- Conceder permissões
EXEC sp_addrolemember 'db_datareader', 'kurier_user'
EXEC sp_addrolemember 'db_datawriter', 'kurier_user'
PRINT '✅ Permissões concedidas ao kurier_user'

-- STEP 3: Criar tabelas de integração
PRINT ''
PRINT '📊 Criando tabelas de integração...'

-- Tabela para armazenar distribuições
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'KURIER_Distribuicoes')
BEGIN
    CREATE TABLE KURIER_Distribuicoes (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        KurierId NVARCHAR(255) NOT NULL,
        NumeroProcesso NVARCHAR(50) NOT NULL,
        NumeroDocumento NVARCHAR(50),
        TipoDistribuicao NVARCHAR(100),
        Destinatario NVARCHAR(MAX),
        DataDistribuicao DATETIME2 NOT NULL,
        DataLimite DATETIME2,
        Conteudo NVARCHAR(MAX),
        Tribunal NVARCHAR(100),
        Vara NVARCHAR(100),
        Status NVARCHAR(50) DEFAULT 'Pendente',
        DataRecebimento DATETIME2 DEFAULT GETUTCDATE(),
        Confirmada BIT DEFAULT 0,
        DataConfirmacao DATETIME2,
        Observacoes NVARCHAR(MAX),
        CriadoEm DATETIME2 DEFAULT GETUTCDATE(),
        AtualizadoEm DATETIME2 DEFAULT GETUTCDATE()
    )
    
    CREATE UNIQUE INDEX IX_KURIER_Distribuicoes_KurierId ON KURIER_Distribuicoes(KurierId)
    CREATE INDEX IX_KURIER_Distribuicoes_NumeroProcesso ON KURIER_Distribuicoes(NumeroProcesso)
    CREATE INDEX IX_KURIER_Distribuicoes_DataDistribuicao ON KURIER_Distribuicoes(DataDistribuicao)
    CREATE INDEX IX_KURIER_Distribuicoes_Confirmada ON KURIER_Distribuicoes(Confirmada)
    
    PRINT '✅ Tabela KURIER_Distribuicoes criada'
END
ELSE
BEGIN
    PRINT '✅ Tabela KURIER_Distribuicoes já existe'
END

-- Tabela para armazenar publicações
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'KURIER_Publicacoes')
BEGIN
    CREATE TABLE KURIER_Publicacoes (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        KurierId NVARCHAR(255) NOT NULL,
        NumeroProcesso NVARCHAR(50) NOT NULL,
        TipoPublicacao NVARCHAR(100),
        Titulo NVARCHAR(MAX),
        Conteudo NVARCHAR(MAX),
        DataPublicacao DATETIME2 NOT NULL,
        FontePublicacao NVARCHAR(200),
        Tribunal NVARCHAR(100),
        Vara NVARCHAR(100),
        Magistrado NVARCHAR(200),
        Partes NVARCHAR(MAX),
        Advogados NVARCHAR(MAX),
        UrlDocumento NVARCHAR(500),
        Categoria NVARCHAR(100),
        Status NVARCHAR(50) DEFAULT 'Pendente',
        DataRecebimento DATETIME2 DEFAULT GETUTCDATE(),
        Confirmada BIT DEFAULT 0,
        DataConfirmacao DATETIME2,
        Observacoes NVARCHAR(MAX),
        CriadoEm DATETIME2 DEFAULT GETUTCDATE(),
        AtualizadoEm DATETIME2 DEFAULT GETUTCDATE()
    )
    
    CREATE UNIQUE INDEX IX_KURIER_Publicacoes_KurierId ON KURIER_Publicacoes(KurierId)
    CREATE INDEX IX_KURIER_Publicacoes_NumeroProcesso ON KURIER_Publicacoes(NumeroProcesso)
    CREATE INDEX IX_KURIER_Publicacoes_DataPublicacao ON KURIER_Publicacoes(DataPublicacao)
    CREATE INDEX IX_KURIER_Publicacoes_Confirmada ON KURIER_Publicacoes(Confirmada)
    
    PRINT '✅ Tabela KURIER_Publicacoes criada'
END
ELSE
BEGIN
    PRINT '✅ Tabela KURIER_Publicacoes já existe'
END

-- Tabela para monitoramento
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'KURIER_Monitoramento')
BEGIN
    CREATE TABLE KURIER_Monitoramento (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        DataExecucao DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        QuantidadeDistribuicoes INT NOT NULL DEFAULT 0,
        QuantidadePublicacoes INT NOT NULL DEFAULT 0,
        AmostraDistribuicoes NVARCHAR(MAX),
        AmostraPublicacoes NVARCHAR(MAX),
        TempoExecucaoMs INT NOT NULL DEFAULT 0,
        StatusExecucao NVARCHAR(50) NOT NULL DEFAULT 'Sucesso',
        MensagemErro NVARCHAR(MAX),
        ModoExecucao NVARCHAR(20) NOT NULL DEFAULT 'RUN_ONCE',
        SomenteMonitoramento BIT NOT NULL DEFAULT 1,
        CriadoEm DATETIME2 DEFAULT GETUTCDATE()
    )
    
    CREATE INDEX IX_KURIER_Monitoramento_DataExecucao ON KURIER_Monitoramento(DataExecucao DESC)
    CREATE INDEX IX_KURIER_Monitoramento_StatusExecucao ON KURIER_Monitoramento(StatusExecucao)
    
    PRINT '✅ Tabela KURIER_Monitoramento criada'
END
ELSE
BEGIN
    PRINT '✅ Tabela KURIER_Monitoramento já existe'
END

-- STEP 4: Criar serviço de integração
PRINT ''
PRINT '🔧 Configurando serviço de integração...'

DECLARE @servicoId BIGINT

INSERT INTO INT_Servico (nome, codigo, descricao, tipo, classe, ativo)
VALUES (
    'Kurier - Integração Distribuições e Publicações',
    'KURIER', 
    'Integração automática com API Kurier para distribuições e publicações jurídicas',
    'WebService',
    'BennerKurierWorker.Service',
    1
)

SET @servicoId = SCOPE_IDENTITY()

PRINT '✅ Serviço KURIER criado com ID: ' + CAST(@servicoId AS VARCHAR(10))

-- STEP 5: Configurar parâmetros do serviço
PRINT ''
PRINT '⚙️ Configurando parâmetros do serviço...'

INSERT INTO INT_ServicoParametro (servico, nome, valor, sigiloso) VALUES
(@servicoId, 'BaseUrl', 'http://www.kurierservicos.com.br/wsservicos/', 0),
(@servicoId, 'LoginDistribuicao', 'o.de.quadro.distribuicao', 1),
(@servicoId, 'SenhaDistribuicao', '855B07EB-99CE-46F1-81CC-4785B090DD72', 1),
(@servicoId, 'LoginJuridico', 'osvaldoquadro', 1),
(@servicoId, 'SenhaJuridico', '159811', 1),
(@servicoId, 'TimeoutSeconds', '30', 0),
(@servicoId, 'MaxRetries', '2', 0),
(@servicoId, 'UserAgent', 'BennerKurier-Production/1.0', 0),
(@servicoId, 'UseHttpOnly', 'true', 0),
(@servicoId, 'DisableSSL', 'true', 0),
(@servicoId, 'BypassProxy', 'true', 0),
(@servicoId, 'ForceHttp11', 'true', 0),
(@servicoId, 'IntervalMinutes', '5', 0),
(@servicoId, 'PageSize', '100', 0),
(@servicoId, 'DaysToConsult', '7', 0),
(@servicoId, 'ConfirmationBatchSize', '50', 0),
(@servicoId, 'AutoConfirm', 'true', 0),
(@servicoId, 'FetchResumos', 'true', 0),
(@servicoId, 'FetchInteiroTeor', 'false', 0)

PRINT '✅ Parâmetros configurados'

-- STEP 6: Conceder permissões nas tabelas para o usuário kurier_user
PRINT ''
PRINT '🔐 Configurando permissões...'

GRANT SELECT, INSERT, UPDATE, DELETE ON KURIER_Distribuicoes TO kurier_user
GRANT SELECT, INSERT, UPDATE, DELETE ON KURIER_Publicacoes TO kurier_user  
GRANT SELECT, INSERT, UPDATE, DELETE ON KURIER_Monitoramento TO kurier_user

PRINT '✅ Permissões concedidas nas tabelas KURIER'

-- STEP 7: Validação final
PRINT ''
PRINT '📋 VALIDAÇÃO FINAL'
PRINT '=================='

-- Verificar serviço
SELECT 
    '✅ Serviço: ' + s.nome + ' | Status: ' + 
    CASE WHEN s.ativo = 1 THEN 'ATIVO' ELSE 'INATIVO' END as Status
FROM INT_Servico s 
WHERE s.codigo = 'KURIER'

-- Verificar tabelas
SELECT '✅ Tabela: ' + TABLE_NAME + ' criada' as TabelaStatus
FROM INFORMATION_SCHEMA.TABLES 
WHERE TABLE_NAME LIKE 'KURIER_%'

-- Verificar usuário
SELECT '✅ Usuário: kurier_user configurado' as UsuarioStatus
WHERE EXISTS (SELECT 1 FROM sys.database_principals WHERE name = 'kurier_user')

PRINT ''
PRINT '🎉 CONFIGURAÇÃO COMPLETA FINALIZADA!'
PRINT '====================================='
PRINT ''
PRINT '📋 PRÓXIMOS PASSOS:'
PRINT '1. Reiniciar o Monitor de Serviços do Benner'
PRINT '2. Verificar se o serviço KURIER aparece na lista'  
PRINT '3. Executar teste de integração: teste-integracao-benner.ps1'
PRINT '4. Monitorar logs em: C:\BennerKurierWorker\logs\'
PRINT ''
PRINT '📊 TABELAS CRIADAS:'
PRINT '- KURIER_Distribuicoes (para armazenar distribuições)'
PRINT '- KURIER_Publicacoes (para armazenar publicações)'
PRINT '- KURIER_Monitoramento (para relatórios)'
PRINT ''
PRINT '🔧 CONNECTION STRING RECOMENDADA:'
PRINT 'Server=10.28.197.21;Database=BENNER_PRODUCAO;User Id=kurier_user;Password=kurier_pass@2025!;TrustServerCertificate=true;Connection Timeout=30;Command Timeout=300;'