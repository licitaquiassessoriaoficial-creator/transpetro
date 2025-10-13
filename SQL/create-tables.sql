-- Scripts SQL para criação das tabelas do BennerKurierWorker
-- Execute estes scripts no SQL Server Management Studio ou similar

-- Criação do banco de dados (opcional - ajuste conforme necessário)
/*
CREATE DATABASE BennerKurier;
GO
USE BennerKurier;
GO
*/

-- ============================================
-- Tabela: Distribuicoes
-- Armazena as distribuições recebidas da Kurier
-- ============================================

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Distribuicoes' AND xtype='U')
BEGIN
    CREATE TABLE Distribuicoes (
        Id NVARCHAR(100) NOT NULL PRIMARY KEY,
        NumeroProcesso NVARCHAR(50) NOT NULL,
        NumeroDocumento NVARCHAR(50) NOT NULL,
        TipoDistribuicao NVARCHAR(100) NOT NULL,
        Destinatario NVARCHAR(200) NOT NULL,
        DataDistribuicao DATETIME2 NOT NULL,
        DataLimite DATETIME2 NULL,
        Conteudo NVARCHAR(MAX) NOT NULL,
        Tribunal NVARCHAR(200) NOT NULL,
        Vara NVARCHAR(200) NOT NULL,
        Status NVARCHAR(50) NOT NULL DEFAULT 'Pendente',
        DataRecebimento DATETIME2 NOT NULL DEFAULT GETDATE(),
        Confirmada BIT NOT NULL DEFAULT 0,
        DataConfirmacao DATETIME2 NULL,
        Observacoes NVARCHAR(MAX) NULL
    );

    -- Índices para otimizar consultas
    CREATE INDEX IX_Distribuicoes_NumeroProcesso ON Distribuicoes(NumeroProcesso);
    CREATE INDEX IX_Distribuicoes_DataDistribuicao ON Distribuicoes(DataDistribuicao);
    CREATE INDEX IX_Distribuicoes_Confirmada ON Distribuicoes(Confirmada);
    CREATE INDEX IX_Distribuicoes_DataRecebimento ON Distribuicoes(DataRecebimento);
    CREATE INDEX IX_Distribuicoes_Tribunal ON Distribuicoes(Tribunal);

    PRINT 'Tabela Distribuicoes criada com sucesso';
END
ELSE
BEGIN
    PRINT 'Tabela Distribuicoes já existe';
END
GO

-- ============================================
-- Tabela: Publicacoes
-- Armazena as publicações recebidas da Kurier
-- ============================================

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Publicacoes' AND xtype='U')
BEGIN
    CREATE TABLE Publicacoes (
        Id NVARCHAR(100) NOT NULL PRIMARY KEY,
        NumeroProcesso NVARCHAR(50) NOT NULL,
        TipoPublicacao NVARCHAR(100) NOT NULL,
        Titulo NVARCHAR(500) NOT NULL,
        Conteudo NVARCHAR(MAX) NOT NULL,
        DataPublicacao DATETIME2 NOT NULL,
        FontePublicacao NVARCHAR(200) NOT NULL,
        Tribunal NVARCHAR(200) NOT NULL,
        Vara NVARCHAR(200) NOT NULL,
        Magistrado NVARCHAR(200) NULL,
        Partes NVARCHAR(MAX) NOT NULL,
        Advogados NVARCHAR(MAX) NULL,
        UrlDocumento NVARCHAR(500) NULL,
        Categoria NVARCHAR(100) NOT NULL,
        Status NVARCHAR(50) NOT NULL DEFAULT 'Pendente',
        DataRecebimento DATETIME2 NOT NULL DEFAULT GETDATE(),
        Confirmada BIT NOT NULL DEFAULT 0,
        DataConfirmacao DATETIME2 NULL,
        Observacoes NVARCHAR(MAX) NULL
    );

    -- Índices para otimizar consultas
    CREATE INDEX IX_Publicacoes_NumeroProcesso ON Publicacoes(NumeroProcesso);
    CREATE INDEX IX_Publicacoes_DataPublicacao ON Publicacoes(DataPublicacao);
    CREATE INDEX IX_Publicacoes_Confirmada ON Publicacoes(Confirmada);
    CREATE INDEX IX_Publicacoes_DataRecebimento ON Publicacoes(DataRecebimento);
    CREATE INDEX IX_Publicacoes_Tribunal ON Publicacoes(Tribunal);
    CREATE INDEX IX_Publicacoes_Categoria ON Publicacoes(Categoria);

    PRINT 'Tabela Publicacoes criada com sucesso';
END
ELSE
BEGIN
    PRINT 'Tabela Publicacoes já existe';
END
GO

-- ============================================
-- Views para relatórios e consultas
-- ============================================

-- View para distribuições pendentes
IF EXISTS (SELECT * FROM sys.views WHERE name = 'vw_DistribuicoesPendentes')
    DROP VIEW vw_DistribuicoesPendentes;
GO

CREATE VIEW vw_DistribuicoesPendentes AS
SELECT 
    Id,
    NumeroProcesso,
    TipoDistribuicao,
    Destinatario,
    DataDistribuicao,
    DataLimite,
    Tribunal,
    Vara,
    DataRecebimento,
    CASE 
        WHEN DataLimite IS NOT NULL AND DataLimite < GETDATE() THEN 'Vencida'
        WHEN DataLimite IS NOT NULL AND DataLimite <= DATEADD(day, 3, GETDATE()) THEN 'Próxima ao vencimento'
        ELSE 'No prazo'
    END AS StatusPrazo
FROM Distribuicoes
WHERE Status = 'Pendente' AND Confirmada = 1;
GO

-- View para publicações recentes
IF EXISTS (SELECT * FROM sys.views WHERE name = 'vw_PublicacoesRecentes')
    DROP VIEW vw_PublicacoesRecentes;
GO

CREATE VIEW vw_PublicacoesRecentes AS
SELECT 
    Id,
    NumeroProcesso,
    TipoPublicacao,
    Titulo,
    DataPublicacao,
    Tribunal,
    Categoria,
    DataRecebimento
FROM Publicacoes
WHERE DataPublicacao >= DATEADD(day, -30, GETDATE())
AND Confirmada = 1;
GO

-- ============================================
-- Stored Procedures para operações comuns
-- ============================================

-- Procedure para obter estatísticas
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_ObterEstatisticas')
    DROP PROCEDURE sp_ObterEstatisticas;
GO

CREATE PROCEDURE sp_ObterEstatisticas
AS
BEGIN
    SELECT 
        'Distribuições' AS Tipo,
        COUNT(*) AS Total,
        SUM(CASE WHEN Confirmada = 0 THEN 1 ELSE 0 END) AS NaoConfirmadas,
        SUM(CASE WHEN Status = 'Pendente' THEN 1 ELSE 0 END) AS Pendentes
    FROM Distribuicoes
    
    UNION ALL
    
    SELECT 
        'Publicações' AS Tipo,
        COUNT(*) AS Total,
        SUM(CASE WHEN Confirmada = 0 THEN 1 ELSE 0 END) AS NaoConfirmadas,
        SUM(CASE WHEN Status = 'Pendente' THEN 1 ELSE 0 END) AS Pendentes
    FROM Publicacoes;
END
GO

-- ============================================
-- Procedure para limpeza de dados antigos
-- ============================================

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_LimpezaDadosAntigos')
    DROP PROCEDURE sp_LimpezaDadosAntigos;
GO

CREATE PROCEDURE sp_LimpezaDadosAntigos
    @DiasRetencao INT = 365
AS
BEGIN
    DECLARE @DataCorte DATETIME2 = DATEADD(day, -@DiasRetencao, GETDATE());
    
    -- Limpar distribuições antigas já confirmadas
    DELETE FROM Distribuicoes 
    WHERE DataRecebimento < @DataCorte 
    AND Confirmada = 1 
    AND Status != 'Pendente';
    
    DECLARE @DistribuicoesExcluidas INT = @@ROWCOUNT;
    
    -- Limpar publicações antigas já confirmadas
    DELETE FROM Publicacoes 
    WHERE DataRecebimento < @DataCorte 
    AND Confirmada = 1 
    AND Status != 'Pendente';
    
    DECLARE @PublicacoesExcluidas INT = @@ROWCOUNT;
    
    SELECT 
        @DistribuicoesExcluidas AS DistribuicoesExcluidas,
        @PublicacoesExcluidas AS PublicacoesExcluidas,
        @DataCorte AS DataCorteUtilizada;
END
GO

PRINT 'Scripts SQL executados com sucesso!';
PRINT 'Tabelas, views e procedures criadas.';
PRINT '';
PRINT 'Para verificar as tabelas criadas, execute:';
PRINT 'SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME IN (''Distribuicoes'', ''Publicacoes'')';
PRINT '';
PRINT 'Para obter estatísticas, execute:';
PRINT 'EXEC sp_ObterEstatisticas';