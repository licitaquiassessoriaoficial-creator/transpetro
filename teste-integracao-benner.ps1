    # Script de Teste Completo - Integração Benner Server
# Conecta ao servidor Benner 10.28.197.21 e processa dados reais

param(
    [string]$BennerServer = "10.28.197.21",
    [string]$Database = "BENNER_PRODUCAO",
    [string]$UserId = "kurier_user",
    [string]$Password = "kurier_pass"
)

Write-Host "🔗 TESTE INTEGRAÇÃO COMPLETA BENNER SERVER" -ForegroundColor Green
Write-Host "===========================================" -ForegroundColor Yellow
Write-Host "Servidor: $BennerServer" -ForegroundColor Cyan
Write-Host "Database: $Database" -ForegroundColor Cyan
Write-Host "User: $UserId" -ForegroundColor Cyan
Write-Host ""

# Testar conectividade de rede
Write-Host "🌐 Testando conectividade de rede..." -ForegroundColor Blue
try {
    $connection = Test-NetConnection -ComputerName $BennerServer -Port 1433 -WarningAction SilentlyContinue
    if ($connection.TcpTestSucceeded) {
        Write-Host "✅ Conectividade de rede OK (porta 1433)" -ForegroundColor Green
    } else {
        Write-Host "❌ Falha na conectividade - porta 1433 bloqueada" -ForegroundColor Red
        Write-Host "⚠️ Verifique firewall e conectividade de rede" -ForegroundColor Yellow
        exit 1
    }
} catch {
    Write-Host "❌ Erro ao testar conectividade: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Configurar variáveis de ambiente
Write-Host ""
Write-Host "⚙️ Configurando variáveis de ambiente..." -ForegroundColor Blue
$env:MODE = "integration"
$env:RUN_ONCE = "true"
$env:Benner__ConnectionString = "Server=$BennerServer;Database=$Database;User Id=$UserId;Password=$Password;TrustServerCertificate=true;Connection Timeout=30;Command Timeout=300;"

Write-Host "✅ Configurado para modo INTEGRAÇÃO" -ForegroundColor Green
Write-Host "✅ Execução única ativada" -ForegroundColor Green

# Executar integração
Write-Host ""
Write-Host "🚀 Iniciando integração Kurier -> Benner..." -ForegroundColor Blue
Write-Host "Esperado:" -ForegroundColor Yellow
Write-Host "- ✅ Conectado ao banco Benner" -ForegroundColor White
Write-Host "- ✅ X distribuições inseridas" -ForegroundColor White
Write-Host "- ✅ X publicações inseridas" -ForegroundColor White
Write-Host "- ✅ Dados confirmados na Kurier" -ForegroundColor White
Write-Host ""

try {
    dotnet run --configuration Release
    Write-Host ""
    Write-Host "🎉 Integração concluída!" -ForegroundColor Green
} catch {
    Write-Host ""
    Write-Host "❌ Erro durante integração: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "📋 Verifique:" -ForegroundColor Yellow
    Write-Host "1. Usuário 'kurier_user' existe no SQL Server" -ForegroundColor White
    Write-Host "2. Tabelas foram criadas (execute script SQL)" -ForegroundColor White
    Write-Host "3. Permissões de acesso ao banco" -ForegroundColor White
}

Write-Host ""
Write-Host "📊 Se funcionou, verifique no Benner:" -ForegroundColor Cyan
Write-Host "- Monitor de Serviços deve mostrar execução recente" -ForegroundColor White
Write-Host "- Tabelas devem conter novos registros" -ForegroundColor White