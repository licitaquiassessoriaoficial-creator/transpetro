#!/usr/bin/env powershell
# Script para testar o endpoint de relay

Write-Host "🧪 === TESTE DO RELAY KURIER ===" -ForegroundColor Green

# Configurações
$BASE_URL = "http://localhost:8080"  # Para teste local
# $BASE_URL = "https://seu-app.railway.app"  # Para Railway

# Função para testar endpoint
function Test-Endpoint {
    param(
        [string]$Method,
        [string]$Endpoint,
        [string]$Body = "",
        [string]$ContentType = "application/xml"
    )
    
    Write-Host "📡 Testando: $Method $Endpoint" -ForegroundColor Yellow
    
    try {
        $headers = @{
            "Content-Type" = $ContentType
            "User-Agent" = "BennerTestScript/1.0"
        }
        
        if ($Body -eq "") {
            $response = Invoke-RestMethod -Uri "$BASE_URL$Endpoint" -Method $Method -Headers $headers
        } else {
            $response = Invoke-RestMethod -Uri "$BASE_URL$Endpoint" -Method $Method -Body $Body -Headers $headers
        }
        
        Write-Host "✅ Sucesso: $($response | ConvertTo-Json -Depth 2)" -ForegroundColor Green
        return $true
    }
    catch {
        Write-Host "❌ Erro: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

# Testes

Write-Host "`n1. Teste de Status da Aplicação" -ForegroundColor Cyan
Test-Endpoint -Method "GET" -Endpoint "/"

Write-Host "`n2. Teste de Health Check" -ForegroundColor Cyan
Test-Endpoint -Method "GET" -Endpoint "/health"

Write-Host "`n3. Teste de Health Check Kurier" -ForegroundColor Cyan
Test-Endpoint -Method "GET" -Endpoint "/api/kurier/health"

Write-Host "`n4. Teste de Relay (GET)" -ForegroundColor Cyan
Test-Endpoint -Method "GET" -Endpoint "/api/kurier/relay?action=test"

Write-Host "`n5. Teste de Relay (POST com XML)" -ForegroundColor Cyan
$xmlBody = @"
<?xml version="1.0" encoding="UTF-8"?>
<request>
    <action>consultar</action>
    <tipo>distribuicoes</tipo>
</request>
"@

Test-Endpoint -Method "POST" -Endpoint "/api/kurier/relay" -Body $xmlBody

Write-Host "`n🎉 === TESTES CONCLUÍDOS ===" -ForegroundColor Green

Write-Host "`n📋 Instruções para configurar no Benner:" -ForegroundColor Magenta
Write-Host "1. Acesse: Administração → Parâmetros de Serviços" -ForegroundColor White
Write-Host "2. Criar novo serviço:" -ForegroundColor White
Write-Host "   - Nome: Kurier" -ForegroundColor White
Write-Host "   - Tipo: Serviço" -ForegroundColor White
Write-Host "   - Endpoint: https://seu-app.railway.app/api/kurier/relay" -ForegroundColor White
Write-Host "   - Usuário: [suas credenciais Kurier]" -ForegroundColor White
Write-Host "   - Senha: [suas credenciais Kurier]" -ForegroundColor White
Write-Host "   - Ativo: ✓" -ForegroundColor White
Write-Host "   - TLS: ✓" -ForegroundColor White