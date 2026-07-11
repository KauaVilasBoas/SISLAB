# SISLAB — Local Development Setup

Guide to running the SISLAB API against a local PostgreSQL instance. No secrets are stored in the
repository — everything sensitive goes in **User Secrets** of the host project.

## Prerequisites

- .NET 8 SDK
- PostgreSQL running locally on `localhost:5432` with a database named `SISLAB_LOCALHOST` (user `postgres`)

## 1. User Secrets (`src/Host/SISLAB.Api`)

Secrets are already configured in the dev environment. To reconstitute from scratch:

```bash
cd src/Host/SISLAB.Api
dotnet user-secrets init   # only if UserSecretsId is not yet set

# Connection string (write-side EF + Dapper + Lumen — all use the same "SislabDb" key)
dotnet user-secrets set "ConnectionStrings:SislabDb" "Host=localhost;Port=5432;Database=SISLAB_LOCALHOST;Username=postgres;Password=<your-password>"

# Lumen Identity JWT (dev). Secret must be at least 32 characters.
dotnet user-secrets set "LumenIdentity:Jwt:Secret"   "<random-min-32-chars>"
dotnet user-secrets set "LumenIdentity:Jwt:Issuer"   "sislab-local"
dotnet user-secrets set "LumenIdentity:Jwt:Audience" "sislab-local"

# Dev seed — creates the LAFTE demo company + admin user already activated (bypasses email confirmation).
# Admin credentials are NEVER hardcoded — only in User Secrets / env vars.
# Password must satisfy Lumen's policy (min. 12 chars: uppercase, lowercase, digit, special char;
# different from email/username).
dotnet user-secrets set "Seed:Enabled"         "true"
dotnet user-secrets set "Seed:Admin:Email"     "admin@lafte.dev"
dotnet user-secrets set "Seed:Admin:Username"  "lafte-admin"
dotnet user-secrets set "Seed:Admin:Password"  "<strong-password-min-12>"
```

Verify with: `dotnet user-secrets list`.

### Which connection string key does Lumen read?

Lumen (Identity and Authorization) does **NOT** automatically reuse `SislabDb`: the overloads without
an explicit connection string read `ConnectionStrings:DefaultConnection`. To avoid duplicating secrets,
SISLAB resolves `SislabDb` in DI and passes the **string explicitly** to `AddLumenIdentity(connectionString, ...)`
and to the authorization core. That means: **a single `SislabDb` key serves all three** (SISLAB EF,
Lumen Identity, Lumen Authorization). No need to set `DefaultConnection`.

### Lumen options configuration (Jwt/App/Smtp/Hibp)

Lumen Identity binds its option sections (`Jwt`, `App`, `Smtp`, `Hibp`) from the **root** of
`IConfiguration` with `ValidateOnStart` (all sections have `[Required]` fields). To keep the
`LumenIdentity:*` namespace (secrets and appsettings) without polluting the root, the SISLAB DI
re-bases the section: `configuration.GetSection("LumenIdentity")` is passed as Lumen's root
`IConfiguration`. This means:

- `LumenIdentity:Jwt`   → resolves as section `Jwt`   (Secret via User Secrets)
- `LumenIdentity:App`   → resolves as section `App`   (BaseUrl, lockout) — in `appsettings.json`
- `LumenIdentity:Smtp`  → resolves as section `Smtp`  (placeholders in `appsettings.json`)
- `LumenIdentity:Hibp`  → resolves as section `Hibp`  (HaveIBeenPwned) — in `appsettings.json`

> `appsettings.json` only contains placeholders/non-sensitive values. Secrets live only in User Secrets.

## 2. Running the API

```bash
dotnet build
cd src/Host/SISLAB.Api
dotnet run
```

- Swagger (dev): `https://localhost:<port>/swagger`
- Health (public): `GET /health`

On startup, hosted services apply migrations per schema:

- **SISLAB Identity** (`IdentitySchemaMigrationsHostedService`) → schema `tenancy`:
  `companies`, `company_memberships` (+ `__ef_migrations_history`).
- **Lumen Identity** and **Lumen Authorization** → their own schemas via Lumen's hosted services.

## 3. Authentication + tenant flow (E1)

1. `POST /api/auth/register` `{ email, username, password }` → creates the user in Lumen.
2. `POST /api/auth/login` `{ identifier, password }` → returns
   `{ accessToken, refreshToken, expiresIn, tokenType }` (JWT HS256). The identity field is
   `identifier` (email or username), **not** `email`.
3. `GET  /api/companies/mine` (Bearer) → the user's companies (via `company_memberships`).
4. `POST /api/companies/{companyId}/activate` (Bearer) → validates membership (403 if not a member),
   writes an httpOnly + SameSite=Lax cookie `sislab_active_company`. Used for first selection **and** switching.
5. Subsequent requests: `TenantResolutionMiddleware` reads the cookie, re-validates against
   `company_memberships` on every request and populates `ITenantContext.CompanyId`. Active company stays
   **OUTSIDE the JWT** (Option A); switching company **does not require re-login**.
6. `GET  /api/companies/active` (Bearer + cookie) → active company resolved via `ITenantContext`;
   404 when no valid tenant is set (no cookie or cookie for a company the user does not belong to).

### Tenant-scoped authorization via Lumen (`[RequirePermission]`) — #12

Granular authorization is provided by **Lumen.Authorization**; the scope is the SISLAB active company.

**Permission code convention (enforced by Lumen 1.1.0):** the code is always
`<Controller>.<Action>` (controller class name without the `Controller` suffix + action method name,
both in PascalCase as in C#). Lumen's `Permission.Create` **recomputes** the code from controller +
action and **ignores** any explicit string passed to the attribute — passing an explicit code
(`[RequirePermission("companies.read")]`) causes enforcement to compare the attribute code against the
stored `Controller.Action` and **always deny (403)**. Therefore decorating with **`[RequirePermission]`
without a code** is mandatory; discovery writes `Controller.Action` and the handler derives the same.
Codes are typed as constants in `Contracts/Authorization/IdentityPermissions.cs`.

**Protected endpoints (MVC controller — discovery only sees `ControllerActionDescriptor`,
not Minimal API):** `CompanyMembersController` at `/api/admin/companies/active/members`:
- `GET /` → permission `CompanyMembers.ListMembers` (read).
- `GET /{userId}/removal-eligibility` → permission `CompanyMembers.CheckRemovalEligibility` (management).

**How the Administrator profile receives the permissions:** on boot, Lumen's hosted service
(`PermissionDiscoveryAndReconciliationHostedService`) scans the decorated controllers
(`Discovered 2 action(s)...`), materializes each code as a `Permission` (`SyncDiscoveredAsync`) and
**reconciles all permissions into the Administrator profile** (`ReconcileAdministratorAsync` →
`granted 2 new permission(s)`). Idempotent: on the 2nd boot it logs *"Administrator profile already holds
all 2 permission(s)"*. No manual permission seeding needed.

**Pipeline ordering (critical):** `UseSislabTenantResolution` runs **between** `UseAuthentication` and
`UseAuthorization` — the `PermissionAuthorizationHandler` reads the scope (active company) via
`ITenantScopeAccessor` during `UseAuthorization`; if tenant resolution ran after, the scope would be
empty and every tenant-scoped permission would be denied even on the correct company.

**Proof (SISLAB_LOCALHOST):** admin `admin@lafte.dev` has the Administrator profile
tenant-scoped **to LAFTE**; is a member of **ACME** *without* the profile (seeded by `LafteDevSeeder`).
```
# LAFTE active (allow)
POST /api/companies/{LAFTE}/activate                      -> 204
GET  /api/admin/companies/active/members                  -> 200  [ {membershipId, userId} ]
GET  /api/admin/companies/active/members/{id}/removal-... -> 200
# ACME active (deny) — same user, same token, no profile in ACME
POST /api/companies/{ACME}/activate                       -> 204
GET  /api/admin/companies/active/members                  -> 403
GET  /api/admin/companies/active/members/{id}/removal-... -> 403
```

> ⚠️ Run **only one API instance** at a time in dev. Stale processes (previous builds) left running
> re-execute discovery against the same `Lumen` schema and may overwrite/duplicate permissions
> (observed: collision on `ix_lumen_permission_code_unique`). Kill orphaned `dotnet` processes before
> starting a new one.

### CSRF protection (browser / cookie flow) — #61

Because the browser SPA authenticates over cookies (the httpOnly `sislab_active_company` cookie, and
later an httpOnly session cookie), state-changing requests are guarded with ASP.NET Core antiforgery
using the **double-submit-cookie** pattern.

**Token cookie / header:** readable cookie `XSRF-TOKEN` (not httpOnly — the SPA reads it via JS) and
request header `X-XSRF-TOKEN`. The value is an anti-CSRF token, **not** a credential.

1. **Arm CSRF once** (on app bootstrap and after login):
   ```
   GET /api/auth/csrf   -> 204 + Set-Cookie: XSRF-TOKEN=<token> (SameSite=Strict)
   ```
2. **Send the token** on every state-changing request (POST/PUT/PATCH/DELETE):
   ```
   X-XSRF-TOKEN: <value of the XSRF-TOKEN cookie>
   ```
   The browser automatically resends the `XSRF-TOKEN` cookie; the middleware compares cookie vs header.
   A forged cross-site request cannot read the cookie (same-origin policy), so it cannot produce the
   matching header and is rejected with **403** `{ "success": false, "message": "CSRF token validation failed." }`.

**What is exempt (and why):**
- **Safe methods** (GET/HEAD/OPTIONS/TRACE) — read-only, never mutate state.
- **Public auth/infra paths** reached before a session exists: `/api/auth/*` (login, refresh, register,
  password reset, and `GET /api/auth/csrf` itself), `/health`, `/swagger` — no session cookie to ride on yet.
- **Pure-Bearer, non-browser clients** — a request with **no** `XSRF-TOKEN` cookie is treated as a
  non-browser client whose credential (the `Authorization: Bearer` header) is never sent ambiently by a
  browser, so it cannot be a CSRF victim. Such requests skip validation.

**Pipeline ordering:** `CsrfValidationMiddleware` runs **after** `UseAuthentication`/tenant resolution
and **before** `UseAuthorization`, so forged state-changing requests are short-circuited with 403 before
reaching any endpoint. Antiforgery is registered via `AddSislabAntiforgery()` in `Program.cs`.

**Cookie `SameSite`:** the `XSRF-TOKEN` cookie is `SameSite=Strict` (defense in depth). Once the SPA runs
on a cross-origin host (E7) and the session cookie becomes `SameSite=None; Secure`, revisit whether the
XSRF cookie needs relaxing so the SPA can read it after a cross-site navigation.

### CORS / SameSite (dev)

No SPA yet. Dev default: **SameSite=Lax**, `Secure` follows the request (HTTPS). When the React SPA
runs on a different origin (E7), tighten to `SameSite=None; Secure` and configure CORS with
`AllowCredentials` + explicit origin (not `AllowAnyOrigin`).

### Write-side data isolation — global query filter + stamping interceptor (#11)

Authorization (#12) decides *what an authenticated user may do*; this layer guarantees *which rows
exist for a company at all* — the **write-side half of defense-in-depth** (the read-side is the
mandatory `WHERE company_id = @CompanyId` in Dapper). It is independent of Lumen: **Lumen is not
multi-tenant, so its DbContexts (`identity`, `Lumen` schemas) are deliberately OUTSIDE this filter**
— their tables have no `company_id`.

Contributed by `SislabDbContextBase` (Shared/Infrastructure) so every module inherits it for free:

- **Convention:** a tenant-scoped entity implements `ITenantEntity` (SharedKernel marker exposing
  `Guid CompanyId`). Its column must be `company_id NOT NULL` and indexed (add a composite index
  `(company_id, <lookup key>)` per table when the module lands in E3).
- **Global query filter:** every mapped `ITenantEntity` gets
  `e => ctx.TenantFilterBypassed || e.CompanyId == ctx.TenantFilterCompanyId`. The predicate reads
  context instance members (EF-recommended) so the compiled model is cached once yet each request
  filters by its own `ITenantContext.CompanyId`. **A user never sees another company's rows even
  before authorization runs.**
- **Stamping interceptor (`TenantStampingInterceptor`):** on `SaveChanges`, new `ITenantEntity`
  rows are stamped with the active company automatically (developers never set `company_id` by hand);
  a row carrying a *different* company is rejected (cross-tenant write blocked); changing `company_id`
  on an existing row (re-parenting) is rejected. Adding a tenant row with **no active company and no
  bypass fails fast** rather than persisting an orphan.
- **Auditable bypass (`ITenantBypass`):** system/background work (Jobs processing the Outbox,
  cross-tenant alert scans) opens `using bypass.BeginScope("reason")` — the only way to cross tenants.
  Every open/close is logged at Warning level; a blank reason throws. The filter short-circuits while
  a scope is open and isolation is restored on dispose (re-entrant, depth-counted).

The Identity module's own `Company`/`CompanyMembership` are intentionally **not** `ITenantEntity`:
`Company` *is* the tenant, and membership lookups must span the user's companies (the login/company
picker would break under a per-company filter). Proven by `TenantIsolationTests`
(Shared/Infrastructure.Tests): read isolation A≠B, auto-stamp, cross-tenant block, orphan fail-fast,
and bypass. First real consumers arrive with Inventory (E3).

## 4. Database schemas and migrations on startup

On startup, hosted services apply migrations per DbContext. Expected state after booting against a
clean database (validated on SISLAB_LOCALHOST):

| Schema | Owner | Tables | History table |
|--------|-------|--------|---------------|
| `tenancy`  | SISLAB (IdentityDbContext) | `companies`, `company_memberships` | `tenancy.__ef_migrations_history` |
| `identity` | Lumen Identity | `Users`, `RefreshTokens`, `EmailConfirmationTokens`, `PasswordResetTokens` | `public."__EFMigrationsHistory"` |
| `Lumen`    | Lumen Authorization | `Permission`, `PermissionGroup`, `Profile`, `PermissionProfile`, `UserProfile` (+ Administrator/User seed) | `public."__EFMigrationsHistory"` |

### Schema `identity` collision — how it was resolved

Lumen.Identity.Migrations.PostgreSQL versions 1.0.0/1.1.0 were broken (missing the `[Migration]`
attribute), which prevented Lumen's schemas from being created on boot. **Fixed** in packages
`Lumen.Identity.Migrations.PostgreSQL 1.0.1` and `Lumen.Authorization.Migrations.PostgreSQL 1.1.1`
(complete initial migrations with `[Migration]`).

With Lumen Identity now creating tables in schema `identity`, SISLAB — which originally also put
`companies`/`company_memberships` in `identity` — was moved to a dedicated **`tenancy`** schema.
The tables themselves did not clash (Lumen uses PascalCase with double-quotes; SISLAB uses snake_case),
and the history tables also differ, but two DbContexts + two history tables + different casings in the
same schema is fragile and confusing. `tenancy` correctly reflects that SISLAB's multi-tenancy is a
distinct bounded context from Lumen's user identity. `identity` is 100% Lumen's.

### Resetting the dev database

To start from a clean state (no real data), drop the schemas and let startup re-migrate:

```sql
DROP SCHEMA IF EXISTS identity CASCADE;
DROP SCHEMA IF EXISTS tenancy CASCADE;
DROP SCHEMA IF EXISTS "Lumen" CASCADE;
-- optional, if there is residual Lumen history in public:
DROP TABLE IF EXISTS public."__EFMigrationsHistory";
```

## 5. Known Lumen 1.0.0 package defects (worked around / documented)

`register` and `login` work against the database, but there are three defects **inside the package**
(external black-box library — we do not patch the package):

1. **HIBP typed client with no BaseAddress (worked around in SISLAB).** `AddLumenIdentity` registers
   `AddHttpClient<IPwnedPasswordsClient, PwnedPasswordsClient>` then immediately overrides it with
   `AddScoped<...>`, erasing the `BaseAddress` → `register` was returning 500. SISLAB provides
   `SislabPwnedPasswordsClient` (its own typed client with correct `BaseAddress`/`UserAgent` from
   `LumenIdentity:Hibp`) registered after `AddLumenIdentity`, overriding the broken registration.
   Fail-open: HIBP unavailability does not block registration.
2. **Missing email confirmation template.** `register` creates the user successfully but throws 500
   when rendering `EmailConfirmation.html` (embedded resource missing from the package). The user is
   persisted with `IsActive=false` / email unconfirmed. `login` requires `IsActive=true` → email
   confirmation is non-functional in the package.
3. **`ValidationException` returned as 500** (instead of 400) by Lumen endpoints — DX only; no
   functional impact.

### User activation / seed in dev (automated)

SISLAB provides an **idempotent dev seed** (`LafteDevSeeder` + `DevSeedHostedService`, in the Identity
module) that runs on boot behind the flag `Seed:Enabled=true` and ensures — without depending on
`register`/email confirmation:

1. **Company `LAFTE`** (SISLAB aggregate) with deterministic id
   `10000000-0000-0000-0000-00000000000a`.
2. **Admin user** in Lumen Identity created **already active**
   (`User.Create` + `ConfirmEmail()` — bypasses defect (2) above). Credentials from `Seed:Admin:*`.
3. **Membership** admin ↔ LAFTE in `tenancy.company_memberships`.
4. **Profile `Administrator`** (seeded by Lumen, fixed id
   `20000000-0000-0000-0000-000000000001`) assigned to admin **tenant-scoped to LAFTE**
   (`Lumen."UserProfile"."ScopeId" = companyId`).
5. **Company `ACME`** (id `10000000-0000-0000-0000-00000000000b`) with admin as **member but WITHOUT
   the Administrator profile** — exists to prove tenant-scoped enforcement of #12 (with ACME active,
   `[RequirePermission]`-decorated endpoints return 403).

Re-runs do not duplicate (each step checks existence before creating). Seed failures are logged and
**do not** crash the application. Just set the `Seed:*` User Secrets (section 1) and start the API.

Quick validation (with the API running):

```bash
# login with seed credentials → accessToken
curl -s -X POST http://localhost:5121/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"identifier":"admin@lafte.dev","password":"<password>"}'

# user's companies → should list LAFTE
curl -s http://localhost:5121/api/companies/mine -H "Authorization: Bearer <token>"

# activate LAFTE → 204 + Set-Cookie sislab_active_company (httpOnly, SameSite=Lax)
curl -i -X POST http://localhost:5121/api/companies/10000000-0000-0000-0000-00000000000a/activate \
  -H "Authorization: Bearer <token>"
```

> **Manual seed (fallback):** if you prefer to seed via SQL, activate the user with
> `UPDATE identity."Users" SET "IsActive"=true, "EmailConfirmedAt"=now() WHERE "Email"=...`
> and insert the company + membership manually. The automated seeder makes this unnecessary.

> **Lumen endpoint fields:** `register` = `{ email, username, password }`;
> `login` = `{ identifier, password }` (the field is `identifier`, not `email`). Password rules:
> minimum 12 chars with uppercase, lowercase, digit, special character, different from
> email/username and not breached (HIBP).
