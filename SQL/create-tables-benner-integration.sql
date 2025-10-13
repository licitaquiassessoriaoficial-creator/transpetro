-- Script para criar tabelas de integração Benner × Kurier em PostgreSQL
-- Execute este script no banco de dados Benner

-- Tabela para armazenar distribuições da Kurier
CREATE TABLE IF NOT EXISTS distribuicoes (
    id VARCHAR(255) PRIMARY KEY,
    numero_processo VARCHAR(50) NOT NULL,
    numero_documento VARCHAR(50),
    tipo_distribuicao VARCHAR(100),
    destinatario TEXT,
    data_distribuicao TIMESTAMP NOT NULL,
    data_limite TIMESTAMP,
    conteudo TEXT,
    tribunal VARCHAR(100),
    vara VARCHAR(100),
    status VARCHAR(50) DEFAULT 'Pendente',
    data_recebimento TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    confirmada BOOLEAN DEFAULT FALSE,
    data_confirmacao TIMESTAMP,
    observacoes TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Tabela para armazenar publicações da Kurier
CREATE TABLE IF NOT EXISTS publicacoes (
    id VARCHAR(255) PRIMARY KEY,
    numero_processo VARCHAR(50) NOT NULL,
    tipo_publicacao VARCHAR(100),
    titulo TEXT,
    conteudo TEXT,
    data_publicacao TIMESTAMP NOT NULL,
    fonte_publicacao VARCHAR(200),
    tribunal VARCHAR(100),
    vara VARCHAR(100),
    magistrado VARCHAR(200),
    partes TEXT,
    advogados TEXT,
    url_documento TEXT,
    categoria VARCHAR(100),
    status VARCHAR(50) DEFAULT 'Pendente',
    data_recebimento TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    confirmada BOOLEAN DEFAULT FALSE,
    data_confirmacao TIMESTAMP,
    observacoes TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Tabela para relatórios de monitoramento
CREATE TABLE IF NOT EXISTS relatorios_monitoramento (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    data_execucao TIMESTAMP NOT NULL,
    quantidade_distribuicoes INTEGER DEFAULT 0,
    quantidade_publicacoes INTEGER DEFAULT 0,
    amostra_distribuicoes JSONB,
    amostra_publicacoes JSONB,
    status VARCHAR(50) NOT NULL,
    mensagem TEXT,
    tempo_execucao_segundos DECIMAL(10,3),
    ultima_atualizacao_distribuicoes TIMESTAMP,
    ultima_atualizacao_publicacoes TIMESTAMP,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Índices para otimização de consultas
CREATE INDEX IF NOT EXISTS idx_distribuicoes_numero_processo ON distribuicoes(numero_processo);
CREATE INDEX IF NOT EXISTS idx_distribuicoes_data_distribuicao ON distribuicoes(data_distribuicao);
CREATE INDEX IF NOT EXISTS idx_distribuicoes_confirmada ON distribuicoes(confirmada);
CREATE INDEX IF NOT EXISTS idx_distribuicoes_tribunal ON distribuicoes(tribunal);

CREATE INDEX IF NOT EXISTS idx_publicacoes_numero_processo ON publicacoes(numero_processo);
CREATE INDEX IF NOT EXISTS idx_publicacoes_data_publicacao ON publicacoes(data_publicacao);
CREATE INDEX IF NOT EXISTS idx_publicacoes_confirmada ON publicacoes(confirmada);
CREATE INDEX IF NOT EXISTS idx_publicacoes_tribunal ON publicacoes(tribunal);

CREATE INDEX IF NOT EXISTS idx_relatorios_data_execucao ON relatorios_monitoramento(data_execucao);
CREATE INDEX IF NOT EXISTS idx_relatorios_status ON relatorios_monitoramento(status);

-- Trigger para atualizar updated_at automaticamente
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = CURRENT_TIMESTAMP;
    RETURN NEW;
END;
$$ language 'plpgsql';

-- Aplicar trigger nas tabelas
DROP TRIGGER IF EXISTS update_distribuicoes_updated_at ON distribuicoes;
CREATE TRIGGER update_distribuicoes_updated_at 
    BEFORE UPDATE ON distribuicoes 
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

DROP TRIGGER IF EXISTS update_publicacoes_updated_at ON publicacoes;
CREATE TRIGGER update_publicacoes_updated_at 
    BEFORE UPDATE ON publicacoes 
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

-- Comentários nas tabelas
COMMENT ON TABLE distribuicoes IS 'Armazena distribuições recebidas da API Kurier';
COMMENT ON TABLE publicacoes IS 'Armazena publicações recebidas da API Kurier';
COMMENT ON TABLE relatorios_monitoramento IS 'Armazena relatórios de monitoramento da integração';

COMMENT ON COLUMN distribuicoes.confirmada IS 'Indica se a distribuição foi confirmada de volta para a Kurier';
COMMENT ON COLUMN publicacoes.confirmada IS 'Indica se a publicação foi confirmada de volta para a Kurier';