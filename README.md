# Tarjeta Fidelidad — SaaS Multi-Tenant de Fidelización Mobile-First

Plataforma de fidelización para múltiples marcas de comida rápida. PWA instalable en celular,
modelo híbrido (puntos por monto + sellos por producto), cupones digitales en Apple/Google Wallet
y registro móvil sin fricción.

## Stack

| Capa | Tecnología |
|------|-----------|
| Backend | C# / .NET 8 Web API, Entity Framework Core 8 |
| Frontend móvil | React / Next.js (PWA, workbox) |
| Cache / alta concurrencia | Redis (IDistributedCache) |
| Base de datos | PostgreSQL 16, aislamiento multi-tenant por `tenant_id` |
| Wallets | Apple PassKit (.pkpass) / Google Wallet API |

## Estructura (Monorepo)

```
Tarjeta Fidelidad/
├── backend/
│   ├── src/
│   │   ├── Loyalty.Api/            # Endpoints, Health, DI
│   │   ├── Loyalty.Core/           # Dominio puro (entidades, invariantes, sin EF)
│   │   ├── Loyalty.Application/    # Casos de uso, DTOs (Fase 2+)
│   │   ├── Loyalty.Infrastructure/ # EF Core + PostgreSQL, Redis, DbContext factory
│   │   └── Loyalty.Contracts/      # Contratos compartidos
│   ├── tests/Loyalty.UnitTests/
│   └── .config/dotnet-tools.json   # dotnet-ef 8 local
├── infra/docker-compose.yml        # PostgreSQL + Redis
├── mobile/                         # Next.js PWA (Fase 4: registro, saldo, sellos, QR)
└── docs/
```

## Estado actual — Fase 5 (Apple PassKit + Google Wallet) ✅

Motor de pases de fidelidad real para Apple Wallet y Google Wallet, generado desde el estado
vivo del miembro (saldo + sellos + QR) y con tracking persistido.

- **`IWalletPassService` → `WalletPassService`** (Infrastructure): dos emisiones por vía:
  - **Apple PassKit**: `.pkpass` completo (zip con `pass.json` + `manifest.json` SHA1 +
    `signature` PKCS#7 **realmente firmada** vía `SignedCms`). Respuesta `application/vnd.apple.pkpass`.
  - **Google Wallet**: JWT firmado (HMAC-SHA256 dev) con `genericClass`+`genericObject`
    (`barcode` QR, módulos de texto miembro/puntos/sellos) → `https://pay.google.com/gp/v/save/...`.
- **`DevelopmentSigningCertificate`**: cert autofirmado efímero (RSA 2048). **Producción**:
  sustituirlo por el Pass Signing Certificate comercial de Apple (env `LOYALTY_PKCS12_PATH`)
  y la service-account de Google (env `LOYALTY_GOOGLE_KEY`) — **la estructura del pase no cambia**.
- **`WalletController`**: `GET /api/wallet/apple` (descarga .pkpass) y `GET /api/wallet/google`
  (JWT). Ambos requieren **token de miembro** (`actor=member`); rechazan staff con 401.
- **Persistencia**: reusa `WalletPass` (tracking por miembro+provider, `Revision` incrementa en
  cada re-generación, `MarkUpdated`). Índice `(tenant, member, provider)`.
- **PWA** (`/app/cupones`): botones "Guardar en Apple Wallet" (descarga .pkpass) y "Añadir a Google
  Wallet" (JWT add-to-wallet). Consume `lib/api.ts` (`downloadApplePass`, `fetchGooglePass`).

### Verificación Fase 5 (real)
```
GET /api/wallet/google (Bearer miembro)  → 200, {signedJwt, googleAddUrl}
GET /api/wallet/apple  (Bearer miembro)  → 200, application/vnd.apple.pkpass
   zip: pass.json · manifest.json · icon.png · icon@2x.png · logo.png · signature
   manifest.json = {pass.json, icon.png, ...: "<sha1>"}   (PassKit exigido)
   signature = SignedData PKCS#7 (SECUENCIA ASN.1 0x30 0x82 ...; OID 1.2.840.113549.1.7.2)
BD wallet_passes → fila por provider (Apple rev=3, Google rev=0, Status Active)
Build PWA OK · Tests 12 unit + 4 integración verdes
```

> **Nota honesta**: el `.pkpass` y el JWT son estructuralmente válidos y firmados, pero con material
> de desarrollo. Para instalado real en el wallet del usuario hay que usar los certificados/keys
> comerciales del cliente (p.12 Apple + service-account Google); el pipeline está listo para ello.

## Estado anterior — Fase 4 (App Móvil PWA del Consumidor) ✅

El consumidor tiene su **propia app web instalable con datos reales del backend**:

- **`mobile/`**: Next.js 16 + React 19 (App Router), PWA instalable.
  - `app/manifest.ts` → manifest webapp (standalone, portrait, theme, iconos 192/512).
  - `public/sw.js` → Service Worker Workbox (offline parcial: shell network-first + assets cache-first).
  - Navegación `bottom-nav` touch-first (Inicio / Cupones / Ajustes), target 480px celular.
- **Flujo de registro sin fricción** (`/app/register`): celular + nombre + **correo opcional**,
  slug de tenant hunt. Devuelve token de miembro directamente (sesión de consumidor).
- **Dashboard** (`/app`): saldo de puntos, **QR real** (renderizado con `qrcode`), sellos por
  producto con barras de progreso, actualizado en vivo desde la API.
- **Cupones** (`/app/cupones`): placeholder que anticipa Wallet (Fase 5).
- **Ajustes** (`/app/ajustes`): perfil, marca, cerrar sesión.
- **Sesión**: Context + axios, token en `localStorage`, interceptor Bearer, rehidratación al montar.
- **Estado de sesión cliente** en `lib/session.tsx` + cliente HTTP tipado `lib/api.ts`.

### Backend que la habilita (nuevo en Fase 4)

| Endpoint | Auth | Descripción |
|----------|------|-------------|
| `POST /api/members/register` | público | Registro de consumidor → token `actor=member` + perfil |
| `GET /api/members/me` | Bearer miembro | Saldo, sellos, QR, perfil |

- **`MemberService`**: registro idempotente por celular+tenant (re-registro re-emite token, no duplica),
  genera `QrHash` + `QrSecret` de alta entropía, emite JWT `actor=member`.
- **`MembersController`**: `/register` público; `/me` solo con token de miembro (rechaza staff con **401**).
- **`TokenService`** refactor a emisión por claims genéricos (reutilizable para staff y consumidor);
  alt-claims centralizados en `ClaimNames`.
- **CORS** política `PwaClient` (origen Next dev 3000) agregada al backend.

### Verificación Fase 4 (end-to-end real)

```
POST /api/members/register (nuevo miembro)        → 200, token + qrHash M986720
POST /api/members/register (mismo celular)        → 200, mismo memberId (idempotente)
GET  /api/members/me (Bearer miembro)             → 200, saldo 0, sellos SKU-BURGER 0/3
GET  /api/members/me (Bearer staff/cajero)        → 401 (actor != member)
POST /api/pos/sales (qrHash real M986720)         → 200, +4000 pts, sellos 2/3
GET  /api/members/me (después)                    → saldo 4000, sellos 2/3 (en vivo)
CORS preflight PWA (Origin :3000)                 → 204, allow-origin ok
```

- **PWA servida en `http://127.0.0.1:3000`** (`npx next start -p 3000`): `/` redirige a `/app`,
  `/manifest.webmanifest` 200, `/sw.js` 200, `/icons/*` 200, rutas `/app/*` 200.
- **Build prod** Next: 0 errores TS, 9 páginas estáticas generadas.
- **Tests backend** siguen verdes (12 unit + 4 integración).

## Estado anterior — Fase 3 (Auth JWT + RBAC + Anti-Brute-Force) ✅

- **`password-Hashing`**: `PasswordHasherService` (PBKDF2/SHA-256, 100k iteraciones, salt 16B,
  `CryptographicOperations.FixedTimeEquals`). Sin ASP.NET Identity para no arrastrar dependencias.
- **`TokenService`**: access JWT (HMAC-SHA256, claims sub/role/tenant_id/store_id, 60 min) +
  refresh opaco rotativo (32B aleatorios, solo su HASH SHA-256 se guarda en BD).
- **`AuthService`** + `POST /api/auth`: login / refresh (rotación por familia predicta replay) /
  logout / me. Respuesta uniforme **401** para credenciales inválidas (no expone si el email existe).
- **`LoginThrottleService`** (anti-fuerza-bruta): INCR/EXPIRE **atómicos** de Redis, 5 fallos en
  5 min → bloqueo 15 min (email+IP), devuelve **429** + `Retry-After`.
- **`TenantContextMiddleware`**: lee `tenant_id` del **JWT** (no del body) y lo fija en el
  AsyncLocal de persistencia — un cajero solo opera datos de su tenant; limpia tras el request.
- **RBAC**: `[Authorize(Roles="Cajero,TenantAdmin,SuperAdmin")]` en POS; `tenant` del operador vía
  `ICurrentUser`. El POS rechaza QR de otro tenant (**403**). Sin token → **401**.
- **`AuthSeeder`**: crea SuperAdmin/TenantAdmin/Cajero demo (idempotente por email) al arrancar.
- **Migración `AddRefreshTokens`**: tabla `refresh_tokens` (hash único + familia).
- **Tests**: 12 unit (dominio + hasher/tokens) + 4 integración (auth E2E sobre Postgres+Redis reales:
  login, rotación+replay, logout, throttle). Todos verdes.

## Estado anterior — Fase 2 (Modelo Híbrido Transaccional + QR Redis) ✅

- **Agregado `MemberStamp`**: contador de sellos por miembro+SKU, índice único `(tenant, member, sku)`.
- **`ISaleProcessingService` → `SaleProcessingService`**: venta de caja procesada en **UNA transacción ACID**
  (puntos + sellos + auditoría juntos). Combina:
  - Concurrency optimista (`RowVersion`) en `Member`.
  - **Lock pesimista** `SELECT ... FOR UPDATE` en el contador de sellos.
  - **Idempotencia por `reference`** (retries de caja no duplican puntos).
- **`IMemberQrCacheService` → `MemberQrCacheService`**: resolución de QR vía Redis (hot path,
  TTL 10 min), fallback a BD en miss.
- **`POST /api/pos/sales`**: escaneo QR → procesa venta. Verificado E2E:
  - venta normal **200** (puntos + sellos + hito), duplicado **409**, tenant ajeno **403**.
- **Manejo centralizado de excepciones** (`ExceptionHandlingMiddleware`): `DomainException` → 4xx,
  inesperado → 500 logged (sin exponer detalles).
- **Tests**: 8 unit (invariantes de dominio) + 2 integración (ACID + idempotencia) — todos verdes.
- **Hallazgo de entorno (crítico)**: `EnableRetryOnFailure` a nivel DbContext **rompe** las
  transacciones manuales en Npgsql 8 (incluso dentro de `CreateExecutionStrategy`). Se quitó del
  runtime; `SaleProcessingService` envuelve su ACID en `CreateExecutionStrategy()` para resiliencia.
- **Nota operativa**: al recompilar Release con la API corriendo, el `.dll` queda bloqueado por el
  host → el build falla en copiar. Detener la API antes de compilar (o `rm -rf bin/Release/net8.0`).

## Fase 1 (Cimientos) ✅

- **Modelo de dominio multi-tenant** completo en `Loyalty.Core`: Tenant, LoyaltyProgram
  (híbrido puntos + sellos), StampRule, RedemptionReward, Member, WalletPass, Coupon,
  Store, RewardTransaction, AppUser (roles SuperAdmin/TenantAdmin/Cajero).
- **DbContext** con filtro global por `ImmutableQuery` vía **AsyncLocal** (`AmbientTenantAccessor`)
  — un único modelo compilado, el tenant se re-lee en cada query (sin fuga de aislamiento).
- **Índices críticos**: `(tenant_id, qr_hash)` y `(tenant_id, phone)` únicos; RowVersion
  (concurrency optimista) en `members`.
- **Migración inicial** aplicada: 11 tablas en Postgres (docker).
- **docker-compose** levanta PostgreSQL 16 + Redis 7 (healthchecks).
- **Health endpoint** verifica Postgres + Redis en run-time → `{"status":"ok",...}`.

## Arranque rápido

```bash
# 1. Levantar infraestructura
cd infra && docker compose up -d

# 2. Build backend (Nota Window: usar -c Release por WDAC/App Control)
cd backend && dotnet build ../Loyalty.sln -c Release

# 3. Aplicar migraciones (design-time factory lee LOYALTY_DB_CONNECTION o default)
LOYALTY_DB_CONNECTION="Host=127.0.0.1;Port=5433;Database=loyalty;Username=loyalty;Password=loyalty_dev;SslMode=Disable" \
  dotnet tool run dotnet-ef database update --project src/Loyalty.Infrastructure --startup-project src/Loyalty.Infrastructure

# 4. Ejecutar la API (Release evita el bloqueo WDAC del build Debug)
cd src/Loyalty.Api
ASPNETCORE_URLS="http://127.0.0.1:5000" ASPNETCORE_ENVIRONMENT=Development \
  dotnet bin/Release/net8.0/Loyalty.Api.dll

# 5. Health check
curl http://127.0.0.1:5000/api/health   # → {"status":"ok","postgres":true,"redis":true}

# 6. PWA móvil (frontend Next.js)
cd ../../mobile
npm install            # primera vez
npm run build && npx next start -p 3000   # producción (o: npm run dev)
# Ábrela en http://127.0.0.1:3000
```

## Puertos y conflictos

- PostgreSQL del proyecto está mapeado al **host:5433** (no 5432) para no chocar con el
  servidor PostgreSQL NATIVO de Windows que ya ocupa `:5432`. Redis expone `:6379`.
- El build **Debug** de la API se bloquea por el **WDAC (App Control)** del equipo
  (`0x800711C7`). Workaround: ejecutar el binario **Release** (`dotnet bin/Release/net8.0/...`),
  que sí corre. No es un defecto del código.

## Siguientes fases

- **Fase 6** ✅ Panel de cajero (`/staff`) + canje atómico de cupones + auditoría.
- **Fase 7** ✅ Performance: cache del programa en Redis + ProfileMiddleware (medición servidor 65ms → <200ms).
- **Fase 8** (sugerida): emisión automática de cupones por sellos completados + escáner QR del panel.

## Endpoints API

| Método | Ruta | Auth | Descripción |
|--------|------|------|-------------|
| POST | `/api/auth/login` | público | Login staff → access + refresh |
| POST | `/api/auth/refresh` | público | Rotación de sesión (revoca el anterior) |
| POST | `/api/auth/logout` | público | Revoca el refresh |
| GET | `/api/auth/me` | Bearer | Identidad/rol/tenant del token |
| POST | `/api/pos/sales` | Bearer (Cajero/TenantAdmin/SuperAdmin) | Procesa venta por QR |
| POST | `/api/pos/coupons/redeem` | Bearer staff | Canjea cupón (anti-doble-canje) |
| GET | `/api/pos/coupons/redeemed` | Bearer staff | Auditoría de canjes recientes |
| GET | `/api/pos/stores` | Bearer staff | Tiendas activas del tenant |
| POST | `/api/members/register` | público | Registro de consumidor → token miembro + perfil |
| GET | `/api/members/me` | Bearer miembro | Perfil del consumidor (saldo, sellos, QR) |
| GET | `/api/members/coupons` | Bearer miembro | Cupones activos del consumidor |
| GET | `/api/wallet/apple` | Bearer miembro | Descarga .pkpass (Apple Wallet) |
| GET | `/api/wallet/google` | Bearer miembro | JWT Add to Wallet (Google) |
| GET | `/api/health` | público | Estado Postgres + Redis |

Usuarios demo (seed staff): `admin@loyalty.dev`, `tenantadmin@burger.dev`, `cajero@burger.dev` — password `DemoPass!123`.
Consumidor demo: registrado vía app en `/app/register` o `POST /api/members/register` (celular + nombre).