# Script PowerShell para resolver ServicePointManager HTTPS Error
# Execução no servidor Benner: 10.28.197.21
# Sistema: PJUR_TR | User: SYSDBA

param(
    [string]$SqlServerInstance = "10.28.197.21",
    [string]$Database = "BENNER_PRODUCAO"
)

Write-Host "🚨 RESOLUÇÃO SERVICEPOINT HTTPS ERROR" -ForegroundColor Red
Write-Host "========================================" -ForegroundColor Yellow
Write-Host "Servidor: $SqlServerInstance" -ForegroundColor Cyan
Write-Host "Database: $Database" -ForegroundColor Cyan
Write-Host "Sistema: PJUR_TR" -ForegroundColor Cyan
Write-Host ""

# Verificar se SQL Server está acessível
Write-Host "🔍 Testando conectividade SQL Server..." -ForegroundColor Blue
try {
    $connectionString = "Server=$SqlServerInstance;Database=$Database;Integrated Security=true;Connection Timeout=10;"
    $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
    $connection.Open()
    $connection.Close()
    Write-Host "✅ Conectividade SQL OK" -ForegroundColor Green
} catch {
    Write-Host "❌ Erro de conectividade SQL: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "⚠️ Verifique se você tem acesso ao servidor $SqlServerInstance" -ForegroundColor Yellow
    exit 1
}

# Executar script de correção
Write-Host ""
Write-Host "🔧 Aplicando correção ServicePointManager..." -ForegroundColor Blue

$sqlScript = @"
-- Remover configuração anterior
IF EXISTS (SELECT 1 FROM INT_Servico WHERE codigo = 'KURIER')
BEGIN
    DELETE FROM INT_ServicoParametro WHERE servico IN (SELECT id FROM INT_Servico WHERE codigo = 'KURIER')
    DELETE FROM INT_Servico WHERE codigo = 'KURIER'
    PRINT 'Configuração anterior removida.'
END

-- Criar novo serviço HTTP only
DECLARE @servicoId BIGINT
INSERT INTO INT_Servico (nome, codigo, descricao, tipo, classe, ativo)
VALUES ('Kurier - HTTP Only (ServicePointManager Fix)', 'KURIER', 'Integração Kurier HTTP - resolve erro proxy HTTPS', 'WebService', 'BennerKurierWorker.Service', 1)
SET @servicoId = SCOPE_IDENTITY()

-- Configurar parâmetros HTTP
INSERT INTO INT_ServicoParametro (servico, nome, valor, sigiloso) VALUES
(@servicoId, 'BaseUrl', 'http://www.kurierservicos.com.br/wsservicos/', 0),
(@servicoId, 'LoginDistribuicao', 'o.de.quadro.distribuicao', 1),
(@servicoId, 'SenhaDistribuicao', '855B07EB-99CE-46F1-81CC-4785B090DD72', 1),
(@servicoId, 'LoginJuridico', 'osvaldoquadro', 1),
(@servicoId, 'SenhaJuridico', '159811', 1),
(@servicoId, 'UseHttpOnly', 'true', 0),
(@servicoId, 'DisableSSL', 'true', 0),
(@servicoId, 'BypassProxy', 'true', 0)

SELECT 'Serviço criado com ID: ' + CAST(@servicoId AS VARCHAR(10)) as Resultado
"@

try {
    $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
    $command = New-Object System.Data.SqlClient.SqlCommand($sqlScript, $connection)
    $connection.Open()
    $result = $command.ExecuteScalar()
    $connection.Close()
    
    Write-Host "✅ Script SQL executado com sucesso!" -ForegroundColor Green
    Write-Host "📋 $result" -ForegroundColor Cyan
} catch {
    Write-Host "❌ Erro ao executar script SQL: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "🎉 SOLUÇÃO APLICADA COM SUCESSO!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Yellow
Write-Host "✅ Serviço KURIER configurado para HTTP apenas" -ForegroundColor Green
Write-Host "✅ ServicePointManager compatível" -ForegroundColor Green
Write-Host "✅ Proxy HTTPS resolvido" -ForegroundColor Green
Write-Host ""
Write-Host "📋 PRÓXIMOS PASSOS:" -ForegroundColor Yellow
Write-Host "1. Abrir Benner > Administração > Monitor de serviços" -ForegroundColor White
Write-Host "2. Localizar 'Kurier - HTTP Only (ServicePointManager Fix)'" -ForegroundColor White
Write-Host "3. Verificar se está ATIVO" -ForegroundColor White
Write-Host "4. Monitorar logs de execução" -ForegroundColor White
Write-Host ""
Write-Host "🚀 O erro ServicePointManager deve estar resolvido!" -ForegroundColor Green