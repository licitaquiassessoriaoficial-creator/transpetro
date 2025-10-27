# Deploy para Railway - BennerKurierWorker  
# Execute este script para fazer deploy com as configurações corretas

Write-Host "🚀 Iniciando deploy para Railway..." -ForegroundColor Green

# Verificar se está no diretório correto
if (!(Test-Path "BennerKurierWorker.csproj")) {
    Write-Host "❌ Execute este script no diretório raiz do projeto" -ForegroundColor Red
    exit 1
}

# Verificar se git está limpo
$gitStatus = git status --porcelain
if ($gitStatus) {
    Write-Host "⚠️  Existem arquivos não commitados. Fazendo commit automático..." -ForegroundColor Yellow
    git add .
    git commit -m "chore: Atualiza configurações para Railway deploy"
}

# Push para trigger do Railway
Write-Host "📤 Fazendo push para Railway..." -ForegroundColor Blue
git push

Write-Host "✅ Deploy iniciado! Monitore os logs no Railway Dashboard" -ForegroundColor Green
Write-Host ""
Write-Host "🔍 Logs esperados no Railway:" -ForegroundColor Cyan
Write-Host "🔵 Kurier Distribuição configurada: User: o.de.quadro.distribuicao" -ForegroundColor Blue
Write-Host "🟣 Kurier Jurídico configurado: User: osvaldoquadro" -ForegroundColor Magenta
Write-Host ""
Write-Host "🌐 Railway Dashboard: https://railway.app/dashboard" -ForegroundColor Yellow