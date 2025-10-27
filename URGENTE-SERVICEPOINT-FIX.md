# 🚨 SOLUÇÃO URGENTE: ServicePointManager HTTPS Error

## 📍 **Ambiente Identificado:**
- **Benner Server**: 10.28.197.21
- **Benner Provider**: 10.28.197.21  
- **Sistema**: PJUR_TR
- **Usuário**: SYSDBA

## ❌ **Erro Específico:**
```
(&H80131500) The ServicePointManager does not support proxies with the https scheme.
Line: 24
```

## 🎯 **CAUSA RAIZ:**
O ServicePointManager do .NET Framework no Benner Server não consegue processar requisições HTTPS através de proxy corporativo.

## ✅ **SOLUÇÃO IMEDIATA - Execute AGORA:**

### **1. Execute o Script SQL:**
```sql
-- No SQL Server Management Studio conectado ao Benner:
-- Usar o arquivo: SQL/fix-servicepoint-benner-server.sql
```

### **2. Passos no Benner Server (10.28.197.21):**

#### **A. Acessar Monitor de Serviços:**
1. Abra o Benner Sistema PJUR_TR
2. Vá para: **Administração > Monitor de serviços**
3. Localize: **"Publicações Online/Distribuição"**

#### **B. Aplicar Correção:**
1. **DESATIVE** o serviço atual
2. Execute o script SQL: `fix-servicepoint-benner-server.sql`
3. **REATIVE** o serviço  
4. **Monitore** os logs

### **3. Configuração Esperada Após Correção:**
```
✅ BaseUrl: http://www.kurierservicos.com.br/wsservicos/
✅ UseHttpOnly: true
✅ DisableSSL: true  
✅ ForceHttp11: true
✅ TimeoutSeconds: 30
```

## 🔍 **VALIDAÇÃO:**
Após aplicar, o serviço deve mostrar:
- ✅ **Status**: Ativo
- ✅ **Última execução**: Data/hora atual
- ✅ **Sem erros** no log

## 📞 **SUPORTE TÉCNICO:**
Se o erro persistir:
1. Verifique proxy corporativo no servidor 10.28.197.21
2. Confirme conectividade HTTP (porta 80) para kurierservicos.com.br
3. Reinicie o serviço do Benner Server
4. Verifique firewall Windows no servidor

## 🚀 **SISTEMA RAILWAY:**
O worker no Railway funciona independentemente e serve como backup caso o servidor local tenha problemas de conectividade.

---
**⚠️ IMPORTANTE**: Esta correção deve ser aplicada no servidor Benner (10.28.197.21) pelo administrador do sistema.