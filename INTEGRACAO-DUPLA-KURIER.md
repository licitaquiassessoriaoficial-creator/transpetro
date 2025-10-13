# 🔄 Integração Dupla com Kurier

Este documento descreve as alterações implementadas no **BennerKurierWorker** para suportar duas integrações independentes com a Kurier:

## 🎯 Objetivo

Permitir que o sistema trabalhe simultaneamente com:

- **Kurier Distribuição** (KDistribuicao) - Distribuições judiciais
- **Kurier Jurídico** (KJuridico) - Publicações oficiais

## 🏗️ Arquitetura Implementada

### 1. HttpClient Separados

O sistema agora utiliza **IHttpClientFactory** para criar dois clientes HTTP independentes:

```csharp
private readonly HttpClient _httpDistribuicao;  // Para KDistribuicao
private readonly HttpClient _httpJuridico;      // Para KJuridico
```

### 2. Configurações Independentes

**Configuração Kurier Distribuição** (`appsettings.json`):

```json
"Kurier": {
  "BaseUrl": "https://www.kurierservicos.com.br/wsservicos/",
  "Usuario": "o.de.quadro.distribuicao",
  "Senha": "855B07EB-99CE-46F1-81CC-4785B090DD72",
  "TimeoutSeconds": 100,
  "MaxRetries": 3,
  "DelayInicial": 2
}
```

**Configuração Kurier Jurídico** (`appsettings.json`):

```json
"KurierJuridico": {
  "BaseUrl": "https://www.kurierservicos.com.br/wsservicos/",
  "Usuario": "osvaldoquadro",
  "Senha": "159811",
  "TimeoutSeconds": 100,
  "MaxRetries": 3,
  "DelayInicial": 2,
  "ConfirmarPublicacoesKey": "Identificador"
}
```

### 3. Classes de Configuração

- `KurierSettings` - Para integração de Distribuição
- `KurierJuridicoSettings` - Para integração Jurídica

## 🔌 Endpoints Suportados

### 🔵 Kurier Distribuição (KDistribuicao)

| Método | Endpoint | Função |
|--------|----------|--------|
| **GET** | `/api/KDistribuicao/ConsultarQuantidadeDistribuicoesDisponiveis` | Consulta quantidade disponível |
| **GET** | `/api/KDistribuicao/ConsultarDistribuicoes` | Busca novas distribuições |
| **POST** | `/api/KDistribuicao/ConfirmarDistribuicoes` | Confirma leitura de distribuições |
| **GET** | `/api/KDistribuicao/ConsultarDistribuicoesConfirmadas` | Histórico de confirmadas |

### 🟣 Kurier Jurídico (KJuridico)

| Método | Endpoint | Função |
|--------|----------|--------|
| **GET** | `/api/KJuridico/ConsultarQuantidadePublicacoesDisponiveis` | Consulta quantidade disponível |
| **GET** | `/api/KJuridico/ConsultarPublicacoes` | Busca novas publicações |
| **POST** | `/api/KJuridico/ConfirmarPublicacoes` | Confirma leitura de publicações |
| **GET** | `/api/KJuridico/ConsultarPublicacoesPersonalizado` | Busca personalizada por filtros |

## 🚀 Funcionalidades Implementadas

### ✅ Autenticação Separada

- Cada integração usa suas próprias credenciais
- Basic Auth independente para cada HttpClient

### ✅ Logs Diferenciados

- **🔵** Kurier Distribuição (produção)
- **🟣** Kurier Jurídico (produção)
- **📦** Distribuições encontradas: X
- **📜** Publicações encontradas: X
- **🟩** Confirmação enviada à Kurier (Distribuição)
- **🟩** Confirmação enviada à Kurier (Jurídico)

### ✅ Retry Policy com Polly

- Retry automático em caso de falha de rede
- Circuit Breaker para evitar sobrecarga
- Backoff exponencial: 2, 4, 8 segundos

### ✅ Tratamento de Erros

- `HttpRequestException` para erros de rede
- Logs detalhados de sucesso e falha
- `EnsureSuccessStatusCode()` em todas as respostas

## 🔧 Registro de Dependências

No `Program.cs`, os serviços são registrados da seguinte forma:

```csharp
// Configurações das duas integrações
services.Configure<KurierSettings>(configuration.GetSection("Kurier"));
services.Configure<KurierJuridicoSettings>(configuration.GetSection("KurierJuridico"));

// HttpClients nomeados com Polly
services.AddHttpClient("KurierDistribuicao", client => { ... })
    .AddPolicyHandler(GetRetryPolicy())
    .AddPolicyHandler(GetCircuitBreakerPolicy());

services.AddHttpClient("KurierJuridico", client => { ... })
    .AddPolicyHandler(GetRetryPolicy())
    .AddPolicyHandler(GetCircuitBreakerPolicy());

// KurierClient como Scoped
services.AddScoped<IKurierClient, KurierClient>();
```

## 🌍 Variáveis de Ambiente

O sistema suporta override via variáveis de ambiente:

### Kurier Distribuição

- `Kurier__BaseUrl`
- `Kurier__User`
- `Kurier__Pass`

### Kurier Jurídico

- `KurierJuridico__BaseUrl`
- `KurierJuridico__User`
- `KurierJuridico__Pass`

## 📋 Métodos da Interface IKurierClient

### Conexão e Testes

- `TestarConexaoKurierAsync()` - Testa ambas as integrações

### KDistribuicao (Distribuições)

- `ConsultarQuantidadeDistribuicoesAsync()`
- `ConsultarDistribuicoesAsync()`
- `ConfirmarDistribuicoesAsync(IEnumerable<string> numerosProcesso)`
- `ConsultarDistribuicoesConfirmadasAsync(string tipoFiltro, DateTime dataInicial, DateTime dataFinal)`

### KJuridico (Publicações)

- `ConsultarQuantidadePublicacoesAsync()`
- `ConsultarPublicacoesAsync(bool somenteResumos = true)`
- `ConfirmarPublicacoesAsync(IEnumerable<string> idsOuNumerosProcesso)`
- `ConsultarPublicacoesPersonalizadoAsync(DateTime data, string? termo, string? tribunal, string? estado)`

## 🎭 Compatibilidade

✅ **Modos suportados:**

- `MODE=ingest` - Ingestão completa
- `MODE=monitoring` - Monitoramento Railway
- `RUN_ONCE=true` - Execução única

✅ **Plataformas suportadas:**

- Railway (PostgreSQL)
- Desenvolvimento local
- Windows Service

## 📈 Benefícios da Implementação

1. **Isolamento**: Cada integração funciona independentemente
2. **Flexibilidade**: Credenciais e configurações separadas
3. **Observabilidade**: Logs específicos para cada módulo
4. **Resiliência**: Retry policy e circuit breaker para cada cliente
5. **Escalabilidade**: Suporte a HttpClientFactory para pool de conexões
6. **Manutenibilidade**: Código bem documentado com XML documentation

## 🔍 Exemplo de Uso

```csharp
// Injeção de dependência
public KurierJobs(IKurierClient kurierClient)
{
    _kurierClient = kurierClient;
}

// Uso dos métodos
var qtdDistribuicoes = await _kurierClient.ConsultarQuantidadeDistribuicoesAsync();
var qtdPublicacoes = await _kurierClient.ConsultarQuantidadePublicacoesAsync();

var distribuicoes = await _kurierClient.ConsultarDistribuicoesAsync();
var publicacoes = await _kurierClient.ConsultarPublicacoesAsync(somenteResumos: true);

await _kurierClient.ConfirmarDistribuicoesAsync(numerosProcesso);
await _kurierClient.ConfirmarPublicacoesAsync(identificadores);
```

---

**✨ Status:** Implementação completa e funcional  
**🧪 Testado:** Compilação bem-sucedida  
**📚 Documentação:** XML documentation em todos os métodos