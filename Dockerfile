# syntax=docker/dockerfile:1
# ============================================================
# Etapa 1: build (SDK). Produce la publicación Release.
# ============================================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restaurar solo el .csproj una vez explota la caché de NuGet.
COPY backend/src/Loyalty.Core/Loyalty.Core.csproj backend/src/Loyalty.Core/
COPY backend/src/Loyalty.Application/Loyalty.Application.csproj backend/src/Loyalty.Application/
COPY backend/src/Loyalty.Infrastructure/Loyalty.Infrastructure.csproj backend/src/Loyalty.Infrastructure/
COPY backend/src/Loyalty.Contracts/Loyalty.Contracts.csproj backend/src/Loyalty.Contracts/
COPY backend/src/Loyalty.Api/Loyalty.Api.csproj backend/src/Loyalty.Api/
RUN dotnet restore backend/src/Loyalty.Api/Loyalty.Api.csproj

# Código + publish
COPY backend/ .
RUN dotnet publish backend/src/Loyalty.Api/Loyalty.Api.csproj \
    -c Release -o /app/publish --no-restore

# ============================================================
# Etapa 2: runtime (non-root, mínima superficie).
# ============================================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Usuario no privilegiado (mejores prácticas de contenedor).
RUN useradd --create-home --shell /usr/sbin/nologin appuser \
    && chown -R appuser:appuser /app
USER appuser

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish ./

EXPOSE 8080
ENTRYPOINT ["dotnet", "Loyalty.Api.dll"]