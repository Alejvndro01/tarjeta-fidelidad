# Cheatsheet de comandos — Tarjeta Fidelidad (Fase 3)

> Proyecto: `C:\Users\Alejvndro01\Desktop\Proyectos Web\Tarjeta Fidelidad`
> Stack: .NET 8 + EF Core + PostgreSQL (docker) + Redis (docker) + JWT/RBAC.
> Todos los comandos asumen git-bash (MSYS) en Windows.

---

## 1. Infraestructura (Docker)

```bash
# Desde el directorio del proyecto
cd infra

# Levantar Postgres (host:5433) + Redis (host:6379)
docker compose up -d
# Si quedaron en "Created" y no "Up" tras el primer boot:
docker compose start <servicio>

# Ver estado y healthchecks
docker compose ps

# Logs
docker compose logs -f postgres

# Bajar TODO (borra volúmenes → datos demo perdidos)
docker compose down -v
```

**Rutas de conexión**: Postgres → `127.0.0.1:5433` (NUNCA 5432, lo ocupa un Postgres nativo de Windows). Redis → `127.0.0.1:6379`. Postgres requiere `SslMode=Disable`.

---

## 2. Build (¡Release obligatorio!)

El WDAC/App Control de este equipo bloquea ejecutar binarios **Debug** (`0x800711C7`). Usa **Release** siempre.

```bash
# Build completo, solo errores
cd backend && dotnet build ../Loyalty.sln -clp:ErrorsOnly

# Build Release de la API
cd src/Loyalty.Api && dotnet build -c Release
```

> ⚠️ Si la API está corriendo, el `Loyalty.Infrastructure.dll` queda bloqueado y el build falla al copiar.
> Detener la API primero, o: `rm -rf bin/Release/net8.0` y rebuild.

---

## 3. Migraciones (dotnet-ef local 8.x)

```bash
cd backend   # el manifest de la tool local vive aquí (.config/dotnet-tools.json)

# Definir la conexión (design-time factory NO recibe --connection)
export LOYALTY_DB_CONNECTION="Host=127.0.0.1;Port=5433;Database=loyalty;Username=loyalty;Password=loyalty_dev;SslMode=Disable"

# Crear migración
dotnet tool run dotnet-ef migrations add <Nombre> \
  --project src/Loyalty.Infrastructure --startup-project src/Loyalty.Infrastructure \
  --output-dir Persistence/Migrations

# Aplicar a la BD dev (loyalty)
dotnet tool run dotnet-ef database update \
  --project src/Loyalty.Infrastructure --startup-project src/Loyalty.Infrastructure

# Aplicar a la BD de tests (loyalty_test) — misma cadena cambiando Database=loyalty_test

# Revertir última / listar
dotnet tool run dotnet-ef migrations remove \
  --project src/Loyalty.Infrastructure --startup-project src/Loyalty.Infrastructure
dotnet tool run dotnet-ef migrations list \
  --project src/Loyalty.Infrastructure --startup-project src/Loyalty.Infrastructure
```

---

## 4. Ejecutar la API

```bash
cd src/Loyalty.Api
ASPNETCORE_URLS="http://127.0.0.1:5000" ASPNETCORE_ENVIRONMENT=Development \
  dotnet bin/Release/net8.0/Loyalty.Api.dll
```

- `Development` → habilita `/swagger`.
- `Production` (por defecto) → sin Swagger, solo API.
- Al arrancar, el `AuthSeeder` crea los usuarios demo si no existen (idempotente).

---

## 5. Verificación rápida

```bash
# Estado Postgres + Redis
curl http://127.0.0.1:5000/api/health
# → {"status":"ok","postgres":true,"redis":true,...}

# Swagger (si corre en Development)
open http://127.0.0.1:5000/swagger
```

---

## 6. Probar auth y POS por curl

Usuarios demo (seed): `admin@loyalty.dev` (SuperAdmin), `tenantadmin@burger.dev`, `cajero@burger.dev` — password `DemoPass!123`.

```bash
APIP=http://127.0.0.1:5000

# 1) Login → devuelve accessToken y refreshToken
curl -s -X POST $APIP/api/auth/login -H "Content-Type: application/json" \
  -d '{"email":"cajero@burger.dev","password":"DemoPass!123"}'
# guarda el accessToken de la respuesta en una variable, ej:
TOKEN="eyJ..."

# 2) Quién soy
curl -s $APIP/api/auth/me -H "Authorization: Bearer $TOKEN"

# 3) Procesar venta por QR (requiere token Cajero/TenantAdmin/SuperAdmin)
curl -s -X POST $APIP/api/pos/sales -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"qrHash":"DEMOQR001","items":[{"productSku":"SKU-BURGER","quantity":1,"unitPrice":1000}],"totalAmount":1000,"reference":"REV-$(date +%s)"}'

# 4) Rotación de sesión (token fresco + revoca el anterior)
curl -s -X POST $APIP/api/auth/refresh -H "Content-Type: application/json" \
  -d '{"refreshToken":"<refreshToken del login>"}'

# 5) Logout (revoca el refresh)
curl -s -X POST $APIP/api/auth/logout -H "Content-Type: application/json" \
  -d '{"refreshToken":"<refreshToken>"}'
```

**Códigos esperados** (comportamiento por diseño):
| Escenario | Código |
|---|---|
| Login correcto | 200 |
| Password incorrecta / usuario inexistente | 401 |
| 5 fallos → bloqueo (throttle) | 429 + `Retry-After` |
| POS sin token | 401 |
| POS con QR de otro tenant | 403 |
| Venta duplicada por `reference` | 409 |
| QR inexistente | 404 |

---

## 7. Tests

```bash
cd backend

# Unit (dominio + hasher/token) → 12/12
dotnet test tests/Loyalty.UnitTests -c Release

# Integración (Postgres + Redis reales) → 4/4
dotnet test tests/Loyalty.IntegrationTests -c Release

# Solo un test/filtro
dotnet test tests/Loyalty.IntegrationTests -c Release --filter "FullyQualifiedName~Auth"

# Consola de Postgres
docker exec -it loyalty-postgres psql -U loyalty -d loyalty
```

> Los tests de integración usan la BD `loyalty_test` y Redis; requieren Docker levantado.

---

## 7b. Inspección directa de BD

```bash
docker exec -it loyalty-postgres psql -U loyalty -d loyalty

# Dentro de psql:
\dt                     # tablas
\d members              # esquema + índices miembros
\d refresh_tokens       # tabla de sesiones
SELECT "Email","Role" FROM app_users;          # usuarios staff
SELECT count(*) FROM refresh_tokens;            # sesiones emitidas/rotadas
```

---

## 8. Solución de problemas del entorno

| Problema | Causa | Fix |
|---|---|---|
| Build falla copiando DLL | API corriendo → `Infrastructure.dll` bloqueado | `process kill` de la API o `rm -rf bin/Release/net8.0` |
| `0x800711C7` al ejecutar | WDAC bloquea Debug | Build y ejecuta **Release** |
| `28P01 auth failed` | Password vieja en el volumen persistido | `docker exec loyalty-postgres psql -U loyalty -d loyalty -c "ALTER ROLE loyalty WITH PASSWORD 'loyalty_dev';"` |
| Conecta a 5432 y falla | Puerto ocupado por Postgres nativo de Windows | Usa `127.0.0.1:5433` |
| `NpgsqlRetryingExecutionStrategy does not support user-initiated transactions` | `EnableRetryOnFailure` activo con `BeginTransactionAsync` manual | Se quitó a nivel DbContext runtime; no volver a activarlo |

---

## 9. Estructura del repo (referencia rápida)

```
Loyalty.sln
backend/
  src/
    Loyalty.Api/            # Controllers (auth/pos/health), Program.cs, middleware
    Loyalty.Core/            # Dominio puro (sin EF)
    Loyalty.Application/     # Interfaces de casos de uso
    Loyalty.Infrastructure/ # EF + Postgres, Redis, Auth, servicios
    Loyalty.Contracts/       # DTOs compartidos
  tests/
    Loyalty.UnitTests/       # 12 tests
    Loyalty.IntegrationTests/ # 4 tests (BD real)
  .config/dotnet-tools.json  # dotnet-ef 8 local
infra/docker-compose.yml     # postgres:16 + redis:7
docs/comandos.md             # este archivo
mobile/                      # Next.js PWA (Fase 4, pendiente)
README.md
```