# BennerKurierWorker - Railway Edition

Worker Service .NET 8 para integração entre o sistema Benner e a plataforma Kurier, **otimizado para execução na nuvem Railway** com suporte a monitoramento diário e execução única.

## 🚀 Principais Funcionalidades

### Modo Tradicional (Desenvolvimento Local)

- **Consulta Contínua**: Executa periodicamente (configurável) consultas aos endpoints da Kurier
- **Sincronização Completa**: Baixa dados completos e confirma recebimento
- **Execução Contínua**: Roda como serviço Windows ou daemon Linux

### Modo Railway (Cloud) - NOVO

- **Execução Única**: Roda uma vez por trigger (ideal para cron jobs)
- **Monitoramento Diário**: Consulta apenas quantidades e amostras de dados
- **Relatórios Estruturados**: Gera logs organizados para Railway Dashboard
- **Variáveis de Ambiente**: Configuração 100% por environment variables

## 📁 Estrutura do Projeto

```text
BennerKurierWorker/
├── Domain/
│   ├── DTOs/
│   │   ├── KurierDistribuicaoDto.cs
│   │   ├── KurierPublicacaoDto.cs
│   │   ├── KurierMonitoramentoDto.cs (NOVO)
│   │   └── KurierCommonDto.cs
│   ├── Distribuicao.cs
│   ├── Publicacao.cs
│   └── RelatorioMonitoramento.cs (NOVO)
├── Infrastructure/
│   ├── IKurierClient.cs (Atualizado)
│   ├── KurierClient.cs (Endpoints de monitoramento)
│   ├── IBennerGateway.cs (Suporte a relatórios)
│   └── BennerPostgreSqlGateway.cs (Persistência PostgreSQL)
├── Application/
│   └── KurierJobs.cs (Modo RUN_ONCE + Monitoramento)
├── Worker/
│   └── Program.cs (Variáveis de ambiente)
├── SQL/
│   ├── create-tables.sql (Original)
│   └── create-tables-railway.sql (NOVO - Cloud)
├── Dockerfile (NOVO - Railway)
├── railway.toml (NOVO - Railway config)
├── Procfile (NOVO - Railway)
├── appsettings.json (Adaptado para Railway)
└── README.md (Este arquivo)
```

## 🌐 Deploy na Railway

### 1. Preparação do Projeto

#### Pré-requisitos

- Conta no [railway.app](https://railway.app)
- Repositório GitHub com o código
- Banco de dados SQL Server (Azure SQL, AWS RDS, etc.)

#### Variáveis de Ambiente Necessárias

```bash
# Configuração da Kurier
Kurier__BaseUrl=https://api.kurier.com.br
Kurier__User=seu_usuario_kurier
Kurier__Password=sua_senha_kurier

# Banco de dados
Benner__ConnectionString=Server=...,Database=...,User Id=...,Password=...

# Modo Railway (IMPORTANTE)
RUN_ONCE=true
DOTNET_ENVIRONMENT=Production
```

### 2. Configuração Inicial na Railway

#### 2.1. Criar Projeto na Railway

1. Acesse [railway.app](https://railway.app) e faça login
2. Clique em **"New Project"**
3. Escolha **"Deploy from GitHub repo"**
4. Conecte seu repositório `BennerKurierWorker`

#### 2.2. Configurar Variáveis de Ambiente

No dashboard da Railway:

1. Vá para **Settings** → **Environment**
2. Adicione as variáveis:

```env
Kurier__BaseUrl=https://api.kurier.com.br
Kurier__User=SEU_USUARIO_AQUI
Kurier__Password=SUA_SENHA_AQUI
Benner__ConnectionString=Server=servidor.database.windows.net;Database=BennerKurier;User Id=usuario;Password=senha;Encrypt=true;TrustServerCertificate=false;Connection Timeout=30;
RUN_ONCE=true
DOTNET_ENVIRONMENT=Production
```

#### 2.3. Configurar Banco de Dados

#### Opção A: Usar banco existente

- Configure a `Benner__ConnectionString` com seu banco atual
- Execute o script `SQL/create-tables-railway.sql` no seu banco

#### Opção B: Novo banco na Railway

1. Na Railway, clique **"New"** → **"Database"** → **"PostgreSQL"**
2. Adapte o código para PostgreSQL (opcional)

### 3. Deploy Manual via CLI

```bash
# Instalar Railway CLI
npm install -g @railway/cli

# Login na Railway
railway login

# Conectar ao projeto
railway link

# Deploy
railway up
```

### 4. Configurar Cron Job Diário

#### 4.1. Na Railway (Recomendado)

1. No dashboard, vá para **Settings** → **Cron**
2. Adicione um cron job:

   ```cron
   0 7 * * * # Executa diariamente às 07:00 UTC
   ```

#### 4.2. Via GitHub Actions (Alternativo)

Crie `.github/workflows/daily-monitor.yml`:

```yaml
name: Daily Kurier Monitor
on:
  schedule:
    - cron: '0 7 * * *' # 07:00 UTC daily
  workflow_dispatch: # Manual trigger

jobs:
  monitor:
    runs-on: ubuntu-latest
    steps:
      - name: Trigger Railway Deployment
        run: |
          curl -X POST https://api.railway.app/graphql \
            -H "Authorization: Bearer ${{ secrets.RAILWAY_TOKEN }}" \
            -H "Content-Type: application/json" \
            -d '{"query":"mutation { deploymentCreate(input: { projectId: \"${{ secrets.RAILWAY_PROJECT_ID }}\", environmentId: \"${{ secrets.RAILWAY_ENV_ID }}\" }) { id } }"}'
```

## ⚙️ Configurações

### appsettings.json (Local/Desenvolvimento)

```json
{
  "Kurier": {
    "BaseUrl": "https://api.kurier.com.br",
    "User": "PLACEHOLDER_KURIER_USER",
    "Password": "PLACEHOLDER_KURIER_PASSWORD"
  },
  "Benner": {
    "ConnectionString": "PLACEHOLDER_CONNECTION_STRING"
  },
  "Jobs": {
    "IntervalMinutes": 5
  },
  "Monitoring": {
    "Enabled": true,
    "ConfirmarNaKurier": false,
    "FetchResumos": true,
    "FetchInteiroTeor": false
  }
}
```

### Configurações de Monitoramento

| Configuração | Descrição | Padrão |
|--------------|-----------|--------|
| `Monitoring.Enabled` | Habilita modo monitoramento | `true` |
| `Monitoring.ConfirmarNaKurier` | Confirma dados na Kurier | `false` |
| `Monitoring.FetchResumos` | Busca apenas resumos (10 amostras) | `true` |
| `Monitoring.FetchInteiroTeor` | Busca dados completos | `false` |

## 🔍 Endpoints da Kurier Consultados

### Modo Monitoramento (Railway)

- `GET /api/KDistribuicao/QuantidadeDistribuicoesDisponiveis`
- `GET /api/KJuridico/ConsultarQuantidadePublicacoesDisponiveis`
- `GET /api/KDistribuicao/resumos` (até 10 amostras)
- `GET /api/KJuridico/ConsultarPublicacoesResumos` (até 10 amostras)

### Modo Tradicional (Local)

- `GET /api/KDistribuicao` (dados completos)
- `GET /api/KJuridico` (dados completos)
- `POST /api/KDistribuicao/confirmar`
- `POST /api/KJuridico/confirmar`

## 📊 Relatórios e Logs

### Logs no Railway Dashboard

O sistema gera logs estruturados visíveis no Railway:

```text
[07:00:01 INF] === RELATÓRIO DE MONITORAMENTO KURIER ===
[07:00:01 INF] Data/Hora: 2024-10-07 07:00:01
[07:00:01 INF] Status: Sucesso
[07:00:01 INF] Tempo Execução: 2.34s
[07:00:01 INF] Distribuições Disponíveis: 15
[07:00:01 INF] Publicações Disponíveis: 8
[07:00:01 INF] Amostra Distribuições: [{"Processo":"1234567-12.2024.8.26.0100","Tipo":"Citação",...}]
[07:00:01 INF] === FIM DO RELATÓRIO ===
```

### Consultas SQL Úteis

```sql
-- Últimos relatórios
SELECT * FROM vw_EstatisticasMonitoramento;

-- Resumo por dia
SELECT * FROM vw_ResumoDiario ORDER BY Data DESC;

-- Estatísticas dos últimos 30 dias
EXEC sp_EstatisticasMonitoramento @DiasConsulta = 30;
```

## 🚨 Troubleshooting

### Problemas Comuns na Railway

1. **Aplicação não inicia**

   ```bash
   # Verificar logs na Railway
   railway logs
   
   # Verificar variáveis de ambiente
   railway variables
   ```

2. **Erro de conexão com banco**

   - Verificar se IP da Railway está liberado no firewall do banco
   - Testar connection string localmente
   - Verificar se `TrustServerCertificate=true` está na connection string

3. **Timeout na execução**

   - Ajustar timeout do banco na connection string: `Connection Timeout=30`
   - Verificar se o endpoint da Kurier está respondendo

4. **Cron job não executa**

   ```text
   - Verificar configuração do cron na Railway
   - Testar execução manual: railway run dotnet BennerKurierWorker.dll
   ```

### Logs de Debug

Para logs mais detalhados, adicione variável de ambiente:

```env
SERILOG__MINIMUMLEVEL__DEFAULT=Debug
```

## 🔄 Execução Local vs Railway

### Local (Desenvolvimento)

```bash
# Configurar RUN_ONCE=false ou omitir
dotnet run
# Executa continuamente a cada 5 minutos
```

### Railway (Produção)

```bash
# RUN_ONCE=true (automático via Dockerfile)
dotnet BennerKurierWorker.dll
# Executa uma vez e encerra
```

## 📈 Escalabilidade

### Recursos Railway Recomendados

- **CPU**: 0.5 vCPU (suficiente para monitoramento)
- **RAM**: 512MB
- **Execução**: Uma vez por dia
- **Custo**: ~$5/mês (Railway Hobby plan)

### Para Maior Volume

Se precisar processar mais dados:

1. Alterar `Monitoring.FetchInteiroTeor` para `true`
2. Alterar `Monitoring.ConfirmarNaKurier` para `true`
3. Aumentar recursos na Railway
4. Considerar execução múltipla por dia

## 🛡️ Segurança

### Variáveis de Ambiente

- ✅ Nunca commitar credenciais no código
- ✅ Usar variáveis de ambiente da Railway
- ✅ Connection strings com SSL: `Encrypt=true`

### Rede

- ✅ Endpoints HTTPS na Kurier
- ✅ Conexão criptografada com banco
- ✅ IPs da Railway liberados no firewall

## 📞 Suporte

### Verificação de Status

```sql
-- Verificar últimas execuções
SELECT TOP 5 * FROM RelatoriosMonitoramento ORDER BY DataExecucao DESC;

-- Verificar se há erros
SELECT * FROM RelatoriosMonitoramento WHERE Status = 'Erro' ORDER BY DataExecucao DESC;
```

### Contato

- **Railway Dashboard**: Logs em tempo real
- **Banco de Dados**: Tabela `RelatoriosMonitoramento`
- **Kurier API**: Status e quantidades disponíveis

---

## 🎯 Exemplo de Uso Completo

### 1. Deploy Inicial

```bash
# 1. Fork/clone do repositório
git clone https://github.com/seu-usuario/BennerKurierWorker
cd BennerKurierWorker

# 2. Configurar Railway
railway login
railway link
railway up

# 3. Configurar variáveis (via dashboard)
# Kurier__BaseUrl, Kurier__User, Kurier__Password, Benner__ConnectionString, RUN_ONCE=true

# 4. Configurar cron diário
# Railway Dashboard > Settings > Cron > "0 7 * * *"
```

### 2. Monitoramento

```bash
# Acompanhar logs
railway logs --follow

# Verificar execução manual
railway run dotnet BennerKurierWorker.dll
```

### 3. Relatórios

```sql
-- No banco de dados
SELECT 
    DataExecucao,
    QuantidadeDistribuicoes,
    QuantidadePublicacoes,
    Status,
    TempoExecucaoSegundos
FROM RelatoriosMonitoramento 
ORDER BY DataExecucao DESC;
```

## ✅ Projeto Pronto

Projeto pronto para produção na Railway! 🚀
 
 
