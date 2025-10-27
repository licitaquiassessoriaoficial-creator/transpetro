# 🎯 GUIA COMPLETO - SETUP KURIER NO BENNER

## ✅ **ARQUIVO CRIADO:**
📄 `SQL/setup-kurier-service-benner.sql`

## 🚀 **COMO EXECUTAR:**

### **Opção A - Interface Gráfica (Recomendado):**

**pgAdmin:**
1. Conecte no banco PostgreSQL do Benner
2. Clique com botão direito no banco → `Query Tool`
3. Abra o arquivo `setup-kurier-service-benner.sql`
4. Pressione **F5** ou clique em **Execute**

**DBeaver:**
1. Conecte no banco PostgreSQL do Benner
2. Clique em `New SQL Script`
3. Cole o conteúdo do arquivo `setup-kurier-service-benner.sql`
4. Pressione **Ctrl+Enter** ou clique em **Execute**

### **Opção B - Linha de Comando:**
```bash
psql "host=SEU_HOST dbname=SEU_BANCO user=SEU_USUARIO password=SUA_SENHA" -f setup-kurier-service-benner.sql
```

## 📋 **RESULTADO ESPERADO:**

### **1. Mensagens de Log:**
```
NOTICE:  ✅ Serviço KURIER criado com ID: X
NOTICE:  ✅ Parâmetro BaseUrl criado
NOTICE:  ✅ Parâmetro UserAgent criado
NOTICE:  ✅ Parâmetro TimeoutSeconds criado
NOTICE:  ✅ Parâmetro MaxRetries criado
NOTICE:  ✅ Parâmetro LoginDistribuicao criado
NOTICE:  ✅ Parâmetro SenhaDistribuicao criado
NOTICE:  ✅ Parâmetro LoginJuridico criado
NOTICE:  ✅ Parâmetro SenhaJuridico criado
NOTICE:  🎯 Setup do serviço KURIER concluído com sucesso!
```

### **2. Consulta de Conferência:**
| parametro | valor_display | sigiloso |
|-----------|---------------|----------|
| BaseUrl | https://www.kurierservicos.com.br/wsservicos/ | 🔓 Não |
| LoginDistribuicao | 🔒 [VALOR SIGILOSO] | 🔐 Sim |
| LoginJuridico | 🔒 [VALOR SIGILOSO] | 🔐 Sim |
| MaxRetries | 3 | 🔓 Não |
| SenhaDistribuicao | 🔒 [VALOR SIGILOSO] | 🔐 Sim |
| SenhaJuridico | 🔒 [VALOR SIGILOSO] | 🔐 Sim |
| TimeoutSeconds | 100 | 🔓 Não |
| UserAgent | BennerKurierWorker/1.0 | 🔓 Não |

## 🔧 **VERIFICAÇÃO NO BENNER:**

### **1. Via Interface Web:**
1. Acesse o Benner
2. Vá em **Administração** → **Parâmetros de serviços**
3. Pesquise por **"Kurier"**
4. Se não aparecer, atualize a página (F5)
5. Se ainda não aparecer, reinicie o app pool/IIS

### **2. Via SQL (verificação manual):**
```sql
-- Verificar serviço criado:
SELECT * FROM "INT_Servico" WHERE codigo = 'KURIER';

-- Verificar parâmetros criados:
SELECT nome, valor, sigiloso 
FROM "INT_ParametroServico" 
WHERE servico_id = (SELECT id FROM "INT_Servico" WHERE codigo = 'KURIER')
ORDER BY nome;
```

## 🔑 **CREDENCIAIS CONFIGURADAS:**

| Parâmetro | Valor | Uso |
|-----------|-------|-----|
| **BaseUrl** | https://www.kurierservicos.com.br/wsservicos/ | URL base da API |
| **UserAgent** | BennerKurierWorker/1.0 | Identificação do client |
| **TimeoutSeconds** | 100 | Timeout das requisições |
| **MaxRetries** | 3 | Máximo de tentativas |
| **LoginDistribuicao** | o.de.quadro.distribuicao | Login para distribuições |
| **SenhaDistribuicao** | 855B07EB-99CE-46F1-81CC-4785B090DD72 | Senha para distribuições |
| **LoginJuridico** | osvaldoquadro | Login para publicações |
| **SenhaJuridico** | 159811 | Senha para publicações |

## 🧪 **TESTE RÁPIDO:**

### **Teste das Credenciais via Postman:**

**1. Teste Distribuição:**
```
GET https://www.kurierservicos.com.br/wsservicos/ConsultarQuantidadeDistribuicoes
Authorization: Basic Auth
Username: o.de.quadro.distribuicao
Password: 855B07EB-99CE-46F1-81CC-4785B090DD72
```

**2. Teste Jurídico:**
```
GET https://www.kurierservicos.com.br/wsservicos/ConsultarQuantidadePublicacoes
Authorization: Basic Auth
Username: osvaldoquadro
Password: 159811
```

## ❌ **POSSÍVEIS PROBLEMAS:**

### **1. Erro de Tabela não encontrada:**
```
ERROR: relation "INT_Servico" does not exist
```
**Solução:** Remover aspas das tabelas no script:
- `"INT_Servico"` → `int_servico`
- `"INT_ParametroServico"` → `int_parametroservico`

### **2. Erro de Permissão:**
```
ERROR: permission denied for relation INT_Servico
```
**Solução:** Executar com usuário administrador do banco

### **3. Serviço não aparece no Benner:**
**Soluções:**
- Atualizar página (F5)
- Reiniciar app pool/IIS do Benner
- Verificar cache do navegador
- Conferir se o usuário tem permissão para ver parâmetros

## 🔄 **APÓS EXECUTAR O SCRIPT:**

### **✅ NO BENNER:**
- [x] Script SQL executado com sucesso
- [ ] Serviço KURIER visível na interface
- [ ] Parâmetros configurados corretamente
- [ ] Permissões verificadas

### **✅ NO KURIER:**
- [ ] Credenciais testadas via Postman
- [ ] APIs respondendo corretamente
- [ ] Autenticação funcionando

### **✅ NO RAILWAY:**
- [ ] Aplicação deployada
- [ ] Logs monitorados
- [ ] Conexão com Benner funcionando
- [ ] Jobs executando

## 🎉 **PRÓXIMOS PASSOS:**
1. Execute o script SQL no Benner
2. Verifique se o serviço aparece na interface
3. Teste as credenciais Kurier
4. Monitore logs do Railway
5. Confirme sincronização de dados

**Tudo pronto para a integração Kurier + Benner! 🚀**