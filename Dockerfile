# Dockerfile para BennerKurierWorker - Railway Compatible
FROM mcr.microsoft.com/dotnet/runtime:8.0-alpine AS base
WORKDIR /app

# Instalar dependências necessárias para SQL Server
RUN apk add --no-cache icu-libs

# Configurar ambiente para invariant globalization (otimização para Alpine)
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

# Copiar arquivo de projeto e restaurar dependências
COPY ["BennerKurierWorker.csproj", "./"]
RUN dotnet restore "BennerKurierWorker.csproj"

# Copiar código fonte e compilar
COPY . .
RUN dotnet build "BennerKurierWorker.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "BennerKurierWorker.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Criar diretório de logs
RUN mkdir -p /app/logs

# Definir variáveis de ambiente padrão para Railway
ENV DOTNET_ENVIRONMENT=Production
ENV ASPNETCORE_ENVIRONMENT=Production
ENV RUN_ONCE=true

# Comando de entrada para Railway
ENTRYPOINT ["dotnet", "BennerKurierWorker.dll"]