# BennerKurierWorker - Deploy Railway

Worker Service .NET 8 para monitoramento diário da API Kurier em modo `RUN_ONCE` na Railway.

## Funcionalidades

### Modo Railway (RUN_ONCE)

- ✅ Execução única por disparo (cron job)
- ✅ Monitoramento sem baixar inteiro teor
- ✅ Não confirma distribuições por padrão
- ✅ Salva contagens e amostras no PostgreSQL
- ✅ Logs estruturados para Railway

### API Kurier

- 8 endpoints oficiais implementados
- Retry policies com backoff exponencial
- Basic Authentication
- Timeout configurável (5min)

### Database

- PostgreSQL na Railway
- Tabela `MonitoramentoKurier` com JSONB
- Índices otimizados para performance
- View para relatórios diários

## Deploy na Railway

### 1. Preparar Repositório

```bash
git init
git add .
git commit -m "Initial commit - BennerKurierWorker Railway"
git branch -M main
git remote add origin <your-repo-url>
git push -u origin main
```

### 2. Criar Projeto na Railway

1. Acesse [railway.app](https://railway.app)
2. Conecte seu repositório GitHub
3. Deploy automático será configurado

### 3. Configurar PostgreSQL

```bash
# Na Railway Dashboard:
# 1. Add Service → Database → PostgreSQL
# 2. Copiar connection string gerada
# 3. Executar script SQL de criação da tabela
```

### 4. Variáveis de Ambiente Railway

```bash
# Configurar na Railway Dashboard → Variables:

# Database (auto-gerada pela Railway)
BENNER__CONNECTIONSTRING=postgresql://username:password@host:port/database

# API Kurier (configurar manualmente)
KURIER__USUARIO=seu_usuario_kurier
KURIER__SENHA=sua_senha_kurier

# Opcional: Override base URL
KURIER__BASEURL=https://api.kurier.com.br

# Railway Mode
RUN_ONCE=true
RAILWAY_ENVIRONMENT=production
```

### 5. Configurar Cron Job

```bash
# Na Railway Dashboard → Settings → Cron
# Executar diariamente às 08:00 UTC:
0 8 * * *
```

### 6. Executar Script SQL

```sql
-- Conectar ao PostgreSQL da Railway e executar:
-- Arquivo: SQL/create-table-monitoramento-railway.postgresql

CREATE TABLE MonitoramentoKurier (
    Id SERIAL PRIMARY KEY,
    DataExecucao TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    QuantidadeDistribuicoes INTEGER NOT NULL DEFAULT 0,
    QuantidadePublicacoes INTEGER NOT NULL DEFAULT 0,
    AmostraDistribuicoes JSONB NULL,
    AmostraPublicacoes JSONB NULL,
    TempoExecucaoMs INTEGER NOT NULL DEFAULT 0,
    StatusExecucao VARCHAR(50) NOT NULL DEFAULT 'Sucesso',
    MensagemErro TEXT NULL,
    ModoExecucao VARCHAR(20) NOT NULL DEFAULT 'RUN_ONCE',
    SomenteMonitoramento BOOLEAN NOT NULL DEFAULT TRUE,
    CriadoEm TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    AtualizadoEm TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);
```

## Monitoramento

### Logs Railway

```bash
# Ver logs em tempo real:
railway logs

# Filtrar por serviço:
railway logs --service <service-name>
```

### Relatórios

```sql
-- Ver execuções recentes:
SELECT DataExecucao, StatusExecucao, QuantidadeDistribuicoes, 
       QuantidadePublicacoes, TempoExecucaoMs
FROM MonitoramentoKurier 
ORDER BY DataExecucao DESC 
LIMIT 10;

-- Relatório diário:
SELECT * FROM vw_MonitoramentoKurierResumo;
```

## Arquitetura

```text
┌─────────────────┐     ┌──────────────┐     ┌─────────────────┐
│   Railway Cron  │────▶│ Worker .NET8 │────▶│  PostgreSQL DB  │
└─────────────────┘     └──────────────┘     └─────────────────┘
                               │
                               ▼
                        ┌──────────────┐
                        │  API Kurier  │
                        └──────────────┘
```

### Fluxo de Execução

1. **Railway Cron** dispara Worker diariamente
2. **Worker** consulta quantidades na API Kurier
3. **Worker** coleta amostras (até 10 registros)
4. **Worker** salva resultado no PostgreSQL
5. **Worker** finaliza execução (RUN_ONCE)

## Configurações

### appsettings.Railway.json

```json
{
  "Monitoring": {
    "Enabled": true,
    "ConfirmarNaKurier": false,
    "FetchResumos": true,
    "FetchInteiroTeor": false
  }
}
```

### Modo RUN_ONCE

- Execução única por disparo
- Não fica em loop infinito
- Ideal para cron jobs
- Finaliza após completar tarefa

## Troubleshooting

### Erro de Conexão Database

```bash
# Verificar variável de ambiente:
echo $BENNER__CONNECTIONSTRING

# Testar conexão:
railway run dotnet run --environment Railway
```

### Erro API Kurier

```bash
# Verificar credenciais:
echo $KURIER__USUARIO
echo $KURIER__SENHA

# Verificar URL:
curl -u "$KURIER__USUARIO:$KURIER__SENHA" https://api.kurier.com.br/distribuicoes/quantidade
```

### Logs de Debug

```bash
# Alterar nível de log na Railway:
LOGGING__LOGLEVEL__DEFAULT=Debug
```

## Performance

### Otimizações Implementadas

- ✅ HttpClient com retry policies
- ✅ Timeouts configuráveis
- ✅ Queries otimizadas (apenas quantidades + amostras)
- ✅ Índices PostgreSQL com GIN para JSONB
- ✅ Execução única (não fica em memória)

### Monitoramento Railway

- CPU: ~10-50% durante execução
- Memória: ~100-200MB
- Execução: ~30-60 segundos
- Network: ~1-5MB por execução

## Próximos Passos

### Futuras Melhorias

- [ ] Gateway para salvar na tabela MonitoramentoKurier
- [ ] Dashboard web para visualização
- [ ] Alertas por email/Slack
- [ ] Métricas avançadas
- [ ] Backup automático dos dados

### Modo Produção Completo

```json
{
  "Monitoring": {
    "ConfirmarNaKurier": true,
    "FetchInteiroTeor": true
  }
}
```
