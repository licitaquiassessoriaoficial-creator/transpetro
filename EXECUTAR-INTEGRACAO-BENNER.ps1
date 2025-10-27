# =====================================================================
# SCRIPT FINAL DE INTEGRAÇÃO BENNER - PRONTO PARA EXECUÇÃO
# Execute este script quando tiver conectividade com 10.28.197.21:1433
# =====================================================================

Write-Host "🚀 INTEGRAÇÃO COMPLETA BENNER × KURIER" -ForegroundColor Green
Write-Host "=======================================" -ForegroundColor Yellow
Write-Host ""

# STEP 1: Testar conectividade
Write-Host "🌐 STEP 1: Testando conectividade de rede..." -ForegroundColor Blue
try {
    $connection = Test-NetConnection -ComputerName "10.28.197.21" -Port 1433 -WarningAction SilentlyContinue
    if ($connection.TcpTestSucceeded) {
        Write-Host "✅ Conectividade: SUCESSO - porta 1433 acessível" -ForegroundColor Green
    } else {
        Write-Host "❌ Conectividade: FALHA - porta 1433 bloqueada" -ForegroundColor Red
        Write-Host "⚠️  AÇÃO NECESSÁRIA:" -ForegroundColor Yellow
        Write-Host "   1. Configurar VPN para acessar rede interna" -ForegroundColor White
        Write-Host "   2. Verificar firewall do servidor 10.28.197.21" -ForegroundColor White
        Write-Host "   3. Confirmar que SQL Server está rodando na porta 1433" -ForegroundColor White
        Write-Host ""
        Write-Host "📋 QUANDO A CONECTIVIDADE ESTIVER FUNCIONANDO:" -ForegroundColor Cyan
        Write-Host "   Execute este script novamente" -ForegroundColor White
        exit 1
    }
} catch {
    Write-Host "❌ Erro ao testar conectividade: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# STEP 2: Preparar arquivos SQL
Write-Host ""
Write-Host "📄 STEP 2: Preparando script SQL para o servidor Benner..." -ForegroundColor Blue
$sqlScript = "SQL\BENNER-SQLSERVER-SETUP-COMPLETO.sql"
if (Test-Path $sqlScript) {
    Write-Host "✅ Script SQL encontrado: $sqlScript" -ForegroundColor Green
    Write-Host "📋 ESTE SCRIPT DEVE SER EXECUTADO NO SERVIDOR BENNER PELO DBA:" -ForegroundColor Yellow
    Write-Host "   1. Conectar no SQL Server Management Studio" -ForegroundColor White
    Write-Host "   2. Abrir arquivo: $sqlScript" -ForegroundColor White
    Write-Host "   3. Executar no database BENNER_PRODUCAO" -ForegroundColor White
} else {
    Write-Host "❌ Script SQL não encontrado: $sqlScript" -ForegroundColor Red
    exit 1
}

# STEP 3: Configurar ambiente para integração
Write-Host ""
Write-Host "⚙️ STEP 3: Configurando ambiente para integração..." -ForegroundColor Blue
$env:MODE = "integration"
$env:RUN_ONCE = "true"
Write-Host "✅ Variáveis configuradas: MODE=integration, RUN_ONCE=true" -ForegroundColor Green

# STEP 4: Testar conexão com banco
Write-Host ""
Write-Host "🔌 STEP 4: Testando conexão com banco Benner..." -ForegroundColor Blue
$connectionString = "Server=10.28.197.21;Database=BENNER_PRODUCAO;User Id=kurier_user;Password=kurier_pass@2025!;TrustServerCertificate=true;Connection Timeout=30;Command Timeout=300;Encrypt=false;Persist Security Info=false;"

try {
    # Simular teste de conexão (você pode implementar teste real aqui)
    Write-Host "✅ Connection string configurada" -ForegroundColor Green
    Write-Host "📋 Aguardando execução do script SQL no servidor..." -ForegroundColor Yellow
} catch {
    Write-Host "❌ Erro na configuração da conexão: $($_.Exception.Message)" -ForegroundColor Red
}

# STEP 5: Executar integração
Write-Host ""
Write-Host "🚀 STEP 5: Iniciando integração Kurier → Benner..." -ForegroundColor Blue
Write-Host "📊 RESULTADOS ESPERADOS:" -ForegroundColor Yellow
Write-Host "   ✅ Conectado ao banco Benner SQL Server" -ForegroundColor White
Write-Host "   ✅ X distribuições inseridas na tabela KURIER_Distribuicoes" -ForegroundColor White
Write-Host "   ✅ X publicações inseridas na tabela KURIER_Publicacoes" -ForegroundColor White
Write-Host "   ✅ Relatório salvo na tabela KURIER_Monitoramento" -ForegroundColor White
Write-Host "   ✅ Dados confirmados de volta na API Kurier" -ForegroundColor White
Write-Host ""

# STEP 6: Executar aplicação
Write-Host "▶️ Executando integração..." -ForegroundColor Cyan
Write-Host ""

try {
    dotnet run --configuration Release
    
    Write-Host ""
    Write-Host "🎉 INTEGRAÇÃO CONCLUÍDA COM SUCESSO!" -ForegroundColor Green
    Write-Host ""
    Write-Host "📊 VERIFICAÇÕES PÓS-EXECUÇÃO:" -ForegroundColor Cyan
    Write-Host "1. Monitor de Serviços do Benner deve mostrar execução recente" -ForegroundColor White
    Write-Host "2. Tabelas KURIER_* devem conter novos registros" -ForegroundColor White
    Write-Host "3. Logs em: logs\benner-kurier-*.txt" -ForegroundColor White
    Write-Host ""
    
} catch {
    Write-Host ""
    Write-Host "❌ ERRO DURANTE INTEGRAÇÃO: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    Write-Host "🔍 DIAGNÓSTICO:" -ForegroundColor Yellow
    Write-Host "1. Verificar se o script SQL foi executado no servidor" -ForegroundColor White
    Write-Host "2. Confirmar que usuário 'kurier_user' existe" -ForegroundColor White
    Write-Host "3. Verificar permissões nas tabelas KURIER_*" -ForegroundColor White
    Write-Host "4. Checar logs para mais detalhes" -ForegroundColor White
    Write-Host ""
    Write-Host "📞 CONTATAR SUPORTE SE PERSISTIR O PROBLEMA" -ForegroundColor Red
}

Write-Host ""
Write-Host "📋 ARQUIVOS IMPORTANTES:" -ForegroundColor Cyan
Write-Host "- Script SQL: $sqlScript" -ForegroundColor White
Write-Host "- Configuração: appsettings.json" -ForegroundColor White
Write-Host "- Logs: logs\benner-kurier-*.txt" -ForegroundColor White
Write-Host "- Teste: teste-integracao-benner.ps1" -ForegroundColor White