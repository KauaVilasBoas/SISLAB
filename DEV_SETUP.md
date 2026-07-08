# SISLAB — Setup de desenvolvimento local

Guia para subir a API do SISLAB contra o Postgres local. Nenhum segredo fica no repositório —
tudo sensível vai em **User Secrets** do projeto Host.

## Pré-requisitos

- .NET 8 SDK
- PostgreSQL local em `localhost:5432` com um banco `SISLAB_LOCALHOST` (user `postgres`).

## 1. User Secrets (projeto `src/Host/SISLAB.Api`)

Os segredos já estão configurados no ambiente de dev. Para reconstituir do zero:

```bash
cd src/Host/SISLAB.Api
dotnet user-secrets init   # se ainda não houver UserSecretsId

# Connection string (write-side EF + Dapper + Lumen — TODOS usam a MESMA chave "SislabDb")
dotnet user-secrets set "ConnectionStrings:SislabDb" "Host=localhost;Port=5432;Database=SISLAB_LOCALHOST;Username=postgres;Password=<sua-senha>"

# JWT da Lumen Identity (dev). Secret com no mínimo 32 chars.
dotnet user-secrets set "LumenIdentity:Jwt:Secret"   "<aleatorio-min-32-chars>"
dotnet user-secrets set "LumenIdentity:Jwt:Issuer"   "sislab-local"
dotnet user-secrets set "LumenIdentity:Jwt:Audience" "sislab-local"

# Seed de desenvolvimento (empresa demo LAFTE + admin). Opt-in via flag.
# As credenciais do admin NUNCA são hardcoded — só existem no seu User Secret/env.
# A senha deve respeitar a política da Lumen (mín. 12 chars: maiúscula, minúscula,
# dígito, caractere especial; distinta de email/username).
dotnet user-secrets set "Seed:Enabled"         "true"
dotnet user-secrets set "Seed:Admin:Email"     "admin@lafte.dev"
dotnet user-secrets set "Seed:Admin:Username"  "lafte-admin"
dotnet user-secrets set "Seed:Admin:Password"  "<senha-forte-min-12-chars>"
```

Conferir: `dotnet user-secrets list`.

### Qual chave de connection string a Lumen lê?

A Lumen (Identity e Authorization) **NÃO** reaproveita `SislabDb` automaticamente: os overloads
sem string leem `ConnectionStrings:DefaultConnection`. Para evitar duplicar segredo, o SISLAB
resolve `SislabDb` no DI e passa a **string explicitamente** para `AddLumenIdentity(connectionString, ...)`
e para o core `AddLumenAuthorization(connectionString, ...)`. Ou seja: **uma única chave `SislabDb`
serve os três** (EF do SISLAB, Lumen Identity, Lumen Authorization). Não é preciso setar
`DefaultConnection`.

### Config de options da Lumen (Jwt/App/Smtp/Hibp)

A Lumen Identity faz bind das seções `Jwt`, `App`, `Smtp`, `Hibp` a partir da **raiz** do
`IConfiguration`, com `ValidateOnStart` (todas têm campos `[Required]`). Para preservar o namespace
`LumenIdentity:*` (e não poluir a raiz), o DI rebaseia a seção: passa
`configuration.GetSection("LumenIdentity")` como `IConfiguration` raiz da Lumen. Assim:

- `LumenIdentity:Jwt`   → seção `Jwt`   da Lumen (Secret via User Secrets)
- `LumenIdentity:App`   → seção `App`   (BaseUrl, lockout) — em `appsettings.json`
- `LumenIdentity:Smtp`  → seção `Smtp`  (placeholders em `appsettings.json`)
- `LumenIdentity:Hibp`  → seção `Hibp`  (HaveIBeenPwned) — em `appsettings.json`

> `appsettings.json` só contém placeholders/valores não sensíveis. Segredos, apenas em User Secrets.

## 2. Subir a API

```bash
dotnet build
cd src/Host/SISLAB.Api
dotnet run
```

- Swagger (dev): `https://localhost:<porta>/swagger`
- Health (público): `GET /health`

No boot, hosted services aplicam migrations por schema:

- **SISLAB Identity** (`IdentitySchemaMigrationsHostedService`) → schema `identity`:
  `companies`, `company_memberships` (+ `__ef_migrations_history`).
- **Lumen Identity** e **Lumen Authorization** → schemas próprios (via hosted services da Lumen).

## 3. Fluxo de autenticação + tenant (E1)

1. `POST /api/auth/register` `{ email, username, password }` → cria usuário na Lumen.
2. `POST /api/auth/login` `{ identifier, password }` → retorna
   `{ accessToken, refreshToken, expiresIn, tokenType }` (JWT HS256). O campo de identidade é
   `identifier` (email ou username), **não** `email`.
3. `GET  /api/companies/mine` (Bearer) → companies do usuário (via `company_memberships`).
4. `POST /api/companies/{companyId}/activate` (Bearer) → valida pertença (403 se não-membro),
   grava cookie **httpOnly + SameSite=Lax** `sislab_active_company`. Serve para 1ª seleção **e** troca.
5. Requests seguintes: `TenantResolutionMiddleware` lê o cookie, revalida contra
   `company_memberships` a cada request e popula `ITenantContext.CompanyId`. Company ativa fica
   **FORA do JWT** (Opção A); trocar de company **não exige novo login**.
6. `GET  /api/companies/active` (Bearer + cookie) → company ativa resolvida (`ITenantContext`);
   404 quando não há tenant válido (sem cookie ou cookie de company que o usuário não pertence).

### CORS / SameSite (dev)

Sem SPA ainda. Default de dev: **SameSite=Lax**, `Secure` = HTTPS da request. Quando o SPA React
subir em origem distinta (E7), endurecer para `SameSite=None; Secure` e configurar CORS com
`AllowCredentials` + origem explícita (não `AllowAnyOrigin`).

## 4. Schemas do banco e migrations no boot

No startup, hosted services aplicam as migrations por DbContext. Estado esperado após o boot
contra um banco limpo (validado no SISLAB_LOCALHOST):

| Schema | Owner | Tabelas | History table |
|--------|-------|---------|---------------|
| `tenancy`  | SISLAB (IdentityDbContext) | `companies`, `company_memberships` | `tenancy.__ef_migrations_history` |
| `identity` | Lumen Identity | `Users`, `RefreshTokens`, `EmailConfirmationTokens`, `PasswordResetTokens` | `public."__EFMigrationsHistory"` |
| `Lumen`    | Lumen Authorization | `Permission`, `PermissionGroup`, `Profile`, `PermissionProfile`, `UserProfile` (+ seed Administrator/User) | `public."__EFMigrationsHistory"` |

### Colisão de schema `identity` — como foi resolvida

As versões 1.0.0/1.1.0 das migrations Postgres da Lumen estavam defeituosas (faltava o atributo
`[Migration]`), o que impedia a criação dos schemas da Lumen no boot. **Corrigido** nos pacotes
`Lumen.Identity.Migrations.PostgreSQL 1.0.1` e `Lumen.Authorization.Migrations.PostgreSQL 1.1.1`
(migrations iniciais completas com `[Migration]`).

Com a Lumen Identity passando a criar tabelas no schema `identity`, o SISLAB — que originalmente
colocava `companies`/`company_memberships` **também** em `identity` — foi movido para um schema
próprio **`tenancy`**. As tabelas em si não colidiam (Lumen usa PascalCase com aspas; SISLAB usa
snake_case) e as history tables também diferem, mas dois DbContexts + dois históricos + casings
distintos no mesmo schema é frágil e confuso. `tenancy` reflete corretamente que a multi-tenancy
do SISLAB é um bounded context distinto da identity de usuários da Lumen. `identity` fica 100%
da Lumen.

### Estado limpo do banco de dev

Para recriar o ambiente do zero (sem dados reais), dropar os schemas e deixar o boot remigrar:

```sql
DROP SCHEMA IF EXISTS identity CASCADE;
DROP SCHEMA IF EXISTS tenancy CASCADE;
DROP SCHEMA IF EXISTS "Lumen" CASCADE;
-- opcional, se houver history residual da Lumen no public:
DROP TABLE IF EXISTS public."__EFMigrationsHistory";
```

## 5. Defeitos conhecidos do pacote Lumen 1.0.0 (contornados/documentados)

O `register` e o `login` da Lumen funcionam contra o banco, mas há três defeitos **dentro do
pacote** (lib externa black-box — não corrigimos o pacote):

1. **HIBP typed client sem BaseAddress (contornado no SISLAB).** `AddLumenIdentity` registra
   `AddHttpClient<IPwnedPasswordsClient, PwnedPasswordsClient>` e, em seguida, o sobrescreve com
   `AddScoped<...>`, anulando o `BaseAddress` → `register` retornava 500. O SISLAB fornece
   `SislabPwnedPasswordsClient` (typed client próprio, `BaseAddress`/`UserAgent` de
   `LumenIdentity:Hibp`) registrado após `AddLumenIdentity`, sobrepondo o registro defeituoso.
   Fail-open: indisponibilidade do HIBP não bloqueia o cadastro.
2. **Template de e-mail de confirmação ausente.** O `register` cria o usuário com sucesso, mas
   lança 500 ao renderizar `EmailConfirmation.html` (recurso embutido inexistente no pacote). O
   usuário fica persistido com `IsActive=false` / e-mail não confirmado. O `login` exige
   `IsActive=true` → a confirmação por e-mail está inoperante no pacote.
3. **`ValidationException` retornada como 500** (em vez de 400) pelos endpoints da Lumen — apenas
   DX; sem impacto funcional.

### Ativação/seed de usuário em dev (automatizado)

O SISLAB provê um **seed de desenvolvimento idempotente** (`LafteDevSeeder` +
`DevSeedHostedService`, no módulo Identity) que roda no boot atrás da flag
`Seed:Enabled=true` e garante, sem depender de `register`/confirmação por e-mail:

1. **Company `LAFTE`** (agregado SISLAB) com Id determinístico
   `10000000-0000-0000-0000-00000000000a`.
2. **Usuário admin** na Lumen Identity criado **já ativo**
   (`User.Create` + `ConfirmEmail()` — contorna o defeito (2) do pacote). Credenciais
   de `Seed:Admin:*` (User Secret/env).
3. **Vínculo** admin ↔ LAFTE em `tenancy.company_memberships`.
4. **Profile `Administrator`** (semeado pela Lumen, Id fixo
   `20000000-0000-0000-0000-000000000001`) atribuído ao admin **tenant-scoped à LAFTE**
   (`Lumen."UserProfile"."ScopeId" = companyId`).

Reexecução não duplica (cada passo checa existência antes de criar). Falha do seed é
logada e **não** derruba o boot. Basta ter os User Secrets `Seed:*` (seção 1) e subir a API.

Validação rápida (com a API no ar):

```bash
# login com as credenciais do seed → accessToken
curl -s -X POST http://localhost:5121/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"identifier":"admin@lafte.dev","password":"<senha>"}'

# companies do admin → deve listar LAFTE
curl -s http://localhost:5121/api/companies/mine -H "Authorization: Bearer <token>"

# ativar LAFTE → 204 + Set-Cookie sislab_active_company (httponly, samesite=lax)
curl -i -X POST http://localhost:5121/api/companies/10000000-0000-0000-0000-00000000000a/activate \
  -H "Authorization: Bearer <token>"
```

> **Seed manual (fallback):** se preferir semear via SQL, ative o usuário com
> `UPDATE identity."Users" SET "IsActive"=true, "EmailConfirmedAt"=now() WHERE "Email"=...`
> e insira company + membership manualmente. O seeder automatizado torna isso desnecessário.

> **Campos dos endpoints da Lumen:** `register` = `{ email, username, password }`;
> `login` = `{ identifier, password }` (o campo é `identifier`, não `email`). Regras de senha:
> mínimo 12 chars, com maiúscula, minúscula, dígito, caractere especial, diferente do
> email/username e não vazada (HIBP).
