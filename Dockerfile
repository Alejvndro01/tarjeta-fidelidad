# syntax=docker/dockerfile:1
# ============================================================
# Etapa 1: build (SDK). Produce la publicación Release.
# ============================================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copia TODO el backend de una vez: el restore necesita la estructura
# de proyectos completa para resolver ProjectReference correctamente.
COPY backend/ ./backend/

# Restaurar con el csproj de la API como entrada (resuelve referencias).
RUN dotnet restore backend/src/Loyalty.Api/Loyalty.Api.csproj

# Publcar Release (--no-restore: ya restaurado, no re-sincroniza).
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