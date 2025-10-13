-- =============================================
-- Script de criação de tabelas para BennerKurierWorker
-- Versão Railway/Cloud com suporte a monitoramento
-- =============================================

-- Criar banco se não existir (opcional - comentado para Railway)
/*
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'BennerKurier')
BEGIN
    CREATE DATABASE BennerKurier;
    PRINT 'Database BennerKurier criado.';
END;
GO
USE BennerKurier;
GO
*/

-- =============================================
-- Tabela de Distribuições
-- =============================================
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Distribuicoes' AND xtype='U')
BEGIN
    CREATE TABLE Distribuicoes (
        Id NVARCHAR(100) PRIMARY KEY,
        NumeroProcesso NVARCHAR(50) NOT NULL,
        NumeroDocumento NVARCHAR(50),
        TipoDistribuicao NVARCHAR(100) NOT NULL,
        Destinatario NVARCHAR(500) NOT NULL,
        DataDistribuicao DATETIME NOT NULL,
        DataLimite DATETIME,
        Conteudo NVARCHAR(MAX) NOT NULL,
        Tribunal NVARCHAR(200) NOT NULL,
        Vara NVARCHAR(200),
        Status NVARCHAR(50) DEFAULT 'Pendente',
        DataRecebimento DATETIME DEFAULT GETDATE(),
        Confirmada BIT DEFAULT 0,
        DataConfirmacao DATETIME,
        Observacoes NVARCHAR(MAX)
    );
    
    PRINT 'Tabela Distribuicoes criada com sucesso.';
END
ELSE
BEGIN
    PRINT 'Tabela Distribuicoes já existe.';
END;
GO

-- =============================================
-- Tabela de Publicações
-- =============================================
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Publicacoes' AND xtype='U')
BEGIN
    CREATE TABLE Publicacoes (
        Id NVARCHAR(100) PRIMARY KEY,
        NumeroProcesso NVARCHAR(50) NOT NULL,
        TipoPublicacao NVARCHAR(100) NOT NULL,
        Titulo NVARCHAR(500) NOT NULL,
        Conteudo NVARCHAR(MAX) NOT NULL,
        DataPublicacao DATETIME NOT NULL,
        FontePublicacao NVARCHAR(200) NOT NULL,
        Tribunal NVARCHAR(200) NOT NULL,
        Vara NVARCHAR(200),
        Magistrado NVARCHAR(200),
        Partes NVARCHAR(MAX) NOT NULL,
        Advogados NVARCHAR(MAX),
        UrlDocumento NVARCHAR(500),
        Categoria NVARCHAR(100) NOT NULL,
        Status NVARCHAR(50) DEFAULT 'Pendente',
        DataRecebimento DATETIME DEFAULT GETDATE(),
        Confirmada BIT DEFAULT 0,
        DataConfirmacao DATETIME,
        Observacoes NVARCHAR(MAX)
    );
    
    PRINT 'Tabela Publicacoes criada com sucesso.';
END
ELSE
BEGIN
    PRINT 'Tabela Publicacoes já existe.';
END;
GO

-- =============================================
-- Tabela de Relatórios de Monitoramento (Nova para Railway)
-- =============================================
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='RelatoriosMonitoramento' AND xtype='U')
BEGIN
    CREATE TABLE RelatoriosMonitoramento (
        Id NVARCHAR(100) PRIMARY KEY,
        DataExecucao DATETIME NOT NULL,
        QuantidadeDistribuicoes INT NOT NULL DEFAULT 0,
        QuantidadePublicacoes INT NOT NULL DEFAULT 0,
        AmostraDistribuicoes NVARCHAR(MAX),
        AmostraPublicacoes NVARCHAR(MAX),
        Status NVARCHAR(50) NOT NULL DEFAULT 'Sucesso',
        Mensagem NVARCHAR(MAX),
        TempoExecucaoSegundos DECIMAL(10,2) NOT NULL DEFAULT 0,
        UltimaAtualizacaoDistribuicoes DATETIME,
        UltimaAtualizacaoPublicacoes DATETIME
    );
    
    PRINT 'Tabela RelatoriosMonitoramento criada com sucesso.';
END
ELSE
BEGIN
    PRINT 'Tabela RelatoriosMonitoramento já existe.';
END;
GO

-- =============================================
-- Índices para performance
-- =============================================

-- Índices para Distribuições
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Distribuicoes_DataDistribuicao')
BEGIN
    CREATE NONCLUSTERED INDEX IX_Distribuicoes_DataDistribuicao 
    ON Distribuicoes (DataDistribuicao DESC);
    PRINT 'Índice IX_Distribuicoes_DataDistribuicao criado.';
END;

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Distribuicoes_Confirmada')
BEGIN
    CREATE NONCLUSTERED INDEX IX_Distribuicoes_Confirmada 
    ON Distribuicoes (Confirmada);
    PRINT 'Índice IX_Distribuicoes_Confirmada criado.';
END;

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Distribuicoes_NumeroProcesso')
BEGIN
    CREATE NONCLUSTERED INDEX IX_Distribuicoes_NumeroProcesso 
    ON Distribuicoes (NumeroProcesso);
    PRINT 'Índice IX_Distribuicoes_NumeroProcesso criado.';
END;

-- Índices para Publicações
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Publicacoes_DataPublicacao')
BEGIN
    CREATE NONCLUSTERED INDEX IX_Publicacoes_DataPublicacao 
    ON Publicacoes (DataPublicacao DESC);
    PRINT 'Índice IX_Publicacoes_DataPublicacao criado.';
END;

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Publicacoes_Confirmada')
BEGIN
    CREATE NONCLUSTERED INDEX IX_Publicacoes_Confirmada 
    ON Publicacoes (Confirmada);
    PRINT 'Índice IX_Publicacoes_Confirmada criado.';
END;

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Publicacoes_NumeroProcesso')
BEGIN
    CREATE NONCLUSTERED INDEX IX_Publicacoes_NumeroProcesso 
    ON Publicacoes (NumeroProcesso);
    PRINT 'Índice IX_Publicacoes_NumeroProcesso criado.';
END;

-- Índices para Relatórios de Monitoramento
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_RelatoriosMonitoramento_DataExecucao')
BEGIN
    CREATE NONCLUSTERED INDEX IX_RelatoriosMonitoramento_DataExecucao 
    ON RelatoriosMonitoramento (DataExecucao DESC);
    PRINT 'Índice IX_RelatoriosMonitoramento_DataExecucao criado.';
END;

-- =============================================
-- Views úteis para Railway/Cloud
-- =============================================

-- View de estatísticas de monitoramento
IF OBJECT_ID('vw_EstatisticasMonitoramento', 'V') IS NOT NULL
    DROP VIEW vw_EstatisticasMonitoramento;
GO

CREATE VIEW vw_EstatisticasMonitoramento AS
SELECT TOP 30
    Id,
    DataExecucao,
    QuantidadeDistribuicoes,
    QuantidadePublicacoes,
    Status,
    TempoExecucaoSegundos,
    UltimaAtualizacaoDistribuicoes,
    UltimaAtualizacaoPublicacoes,
    CASE 
        WHEN Mensagem IS NOT NULL THEN LEFT(Mensagem, 100) + '...'
        ELSE NULL
    END AS MensagemResumo
FROM RelatoriosMonitoramento
ORDER BY DataExecucao DESC;
GO

-- View de resumo diário
IF OBJECT_ID('vw_ResumoDiario', 'V') IS NOT NULL
    DROP VIEW vw_ResumoDiario;
GO

CREATE VIEW vw_ResumoDiario AS
SELECT 
    CAST(DataExecucao AS DATE) AS Data,
    COUNT(*) AS TotalExecucoes,
    SUM(CASE WHEN Status = 'Sucesso' THEN 1 ELSE 0 END) AS ExecucoesSucesso,
    SUM(QuantidadeDistribuicoes) AS TotalDistribuicoes,
    SUM(QuantidadePublicacoes) AS TotalPublicacoes,
    AVG(TempoExecucaoSegundos) AS TempoMedioExecucao
FROM RelatoriosMonitoramento
WHERE DataExecucao >= DATEADD(day, -30, GETDATE())
GROUP BY CAST(DataExecucao AS DATE);
GO

PRINT '==============================================';
PRINT 'Script de criação executado com sucesso!';
PRINT 'Tabelas: Distribuicoes, Publicacoes, RelatoriosMonitoramento';
PRINT 'Views: vw_EstatisticasMonitoramento, vw_ResumoDiario';
PRINT 'Projeto pronto para Railway/Cloud deployment!';
PRINT '==============================================';

-- Exemplos de consultas úteis para Railway logs:
-- SELECT * FROM vw_EstatisticasMonitoramento;
-- SELECT * FROM vw_ResumoDiario ORDER BY Data DESC;
-- SELECT TOP 5 * FROM RelatoriosMonitoramento ORDER BY DataExecucao DESC;