# Deploy para Railway - BennerKurierWorker
# Execute este script para fazer deploy com as configurações corretas

Write-Host "Iniciando deploy para Railway..."

# Verificar se está no diretório correto
if (!(Test-Path "BennerKurierWorker.csproj")) {
    Write-Host "Execute este script no diretório raiz do projeto"
    exit 1
}

# Verificar se git está limpo
$gitStatus = git status --porcelain
if ($gitStatus) {
    Write-Host "Existem arquivos não commitados. Fazendo commit automático..."
    git add .
    git commit -m "chore: Atualiza configurações para Railway deploy"
}

# Push para trigger do Railway
Write-Host "Fazendo push para Railway..."
git push origin main

Write-Host "Deploy iniciado! Monitore os logs no Railway Dashboard"
Write-Host "Logs esperados no Railway:"
Write-Host "Kurier Distribuição configurada: User: o.de.quadro.distribuicao"
Write-Host "Kurier Jurídico configurado: User: osvaldoquadro"
Write-Host "Railway Dashboard: https://railway.app/dashboard"
