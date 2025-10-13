# ðŸš€ Passos para Colocar a IntegraÃ§Ã£o em ProduÃ§Ã£o

## ðŸ“‹ Resumo Executivo

VocÃª tem agora um **Worker .NET 8 completo** que faz integraÃ§Ã£o bidirecional Benner â†” Kurier.

---

## ðŸ¢ **No Sistema Benner**

### 1. **Criar as Tabelas** (5 min)

Execute o script SQL no seu banco PostgreSQL:

```sql
-- Arquivo: SQL/create-tables-railway.sql
CREATE TABLE distribuicoes (...);
CREATE TABLE publicacoes (...);
CREATE TABLE relatorios_monitoramento (...);
```

### 2. **String de ConexÃ£o** (2 min)

Anote sua connection string PostgreSQL:

```text
Server=seu_server;Database=benner;User Id=usuario;Password=senha;
```

---

## ðŸ”— **Na Kurier**

### 1. **Credenciais de API** (2 min)

Anote suas credenciais:

- **Login**: `seu_usuario_kurier`
- **Senha**: `sua_senha_kurier`

### 2. **Testar Conectividade** (3 min)

```bash
curl -u usuario:senha https://www.kurierservicos.com.br/wsservicos/api/KDistribuicao/ConsultarQuantidadeDistribuicoesDisponiveis
```

---

## âš™ï¸ **ConfiguraÃ§Ã£o do Worker**

### 1. **VariÃ¡veis de Ambiente**

```bash
# No Railway ou servidor
Kurier__Usuario=seu_login
Kurier__Senha=sua_senha
Benner__ConnectionString=Server=...;Database=...;
MODE=ingest
RUN_ONCE=true
```

### 2. **Deploy**

- **Railway**: Fazer deploy do projeto com as variÃ¡veis acima
- **Servidor Local**: `dotnet run` com as variÃ¡veis configuradas

---

## ðŸ”„ **O que o Sistema FarÃ¡ Automaticamente**

1. **Busca** distribuiÃ§Ãµes/publicaÃ§Ãµes na Kurier
2. **Salva** no banco Benner (com transaÃ§Ã£o)
3. **Confirma** na Kurier (esvazia as filas)
4. **Gera logs** estruturados

---

## âœ… **Checklist de Go-Live**

- [ ] Tabelas criadas no banco Benner
- [ ] Credenciais Kurier funcionando
- [ ] Worker deployado com variÃ¡veis corretas
- [ ] Primeira execuÃ§Ã£o testada
- [ ] Logs sendo gerados corretamente

### Tempo estimado total: ~15 minutos de configuraÃ§Ã£o

---

## ðŸ†˜ **Se Algo Der Errado**

1. **Verifique os logs** do Worker
2. **Teste as credenciais** Kurier manualmente
3. **Verifique a conexÃ£o** com o banco Benner
4. **Configure** `ConfirmarNaKurier=false` para apenas testar (sem confirmar)

### Ã‰ isso! O sistema estÃ¡ pronto para funcionar. ðŸŽ¯
