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

1. `POST /api/auth/register` → cria usuário na Lumen.
2. `POST /api/auth/login` → retorna `{ accessToken, refreshToken, expiresIn }` (JWT HS256).
3. `GET  /api/companies/mine` (Bearer) → companies do usuário (via `company_user`).
4. `POST /api/companies/{companyId}/activate` (Bearer) → valida pertença (403 se não-membro),
   grava cookie **httpOnly + SameSite=Lax** `sislab_active_company`. Serve para 1ª seleção **e** troca.
5. Requests seguintes: `TenantResolutionMiddleware` lê o cookie, revalida contra `company_user`
   a cada request e popula `ITenantContext.CompanyId`. Company ativa fica **FORA do JWT** (Opção A);
   trocar de company **não exige novo login**.

### CORS / SameSite (dev)

Sem SPA ainda. Default de dev: **SameSite=Lax**, `Secure` = HTTPS da request. Quando o SPA React
subir em origem distinta (E7), endurecer para `SameSite=None; Secure` e configurar CORS com
`AllowCredentials` + origem explícita (não `AllowAnyOrigin`).

## 4. BLOQUEIO CONHECIDO — migrations PostgreSQL da Lumen (1.0.0 / 1.1.0)

Os pacotes NuGet de migrations **PostgreSQL** publicados da Lumen estão **incompletos/defeituosos**;
o schema da Lumen NÃO é criado no boot. Diagnóstico (verificado por reflexão sobre os assemblies):

- `Lumen.Identity.Migrations.PostgreSQL` **1.0.0**: a classe `InitialIdentitySchemaPostgres`
  herda de `Migration` mas **não tem o atributo `[Migration("...")]`** → o scanner do EF Core
  ignora e registra **0 migrations** → nenhuma tabela de identidade é criada
  (log enganoso: "No pending migrations").
- `Lumen.Authorization.Migrations.PostgreSQL` **1.1.0**: contém **apenas** o incremento
  `20260706000000_AddScopeIdToUserProfilePostgres` (um ALTER), **sem** a migration inicial de
  schema → falha com `3F000: schema "Lumen" não existe`.
- `Lumen.Authorization.Migrations.PostgreSQL` **1.0.0**: até tem `InitialLumenAuthorizationSchemaPostgres`
  + seed, mas **também sem `[Migration]`** → 0 migrations. Ou seja, nenhuma versão isolada é
  coerente para PostgreSQL.

**Causa raiz:** os projetos de migration Postgres da Lumen foram escritos à mão sem o `.Designer.cs`
(onde vive o `[Migration]`). É um defeito **dentro do pacote** — não há wiring do consumidor que
conserte um atributo ausente.

**Impacto:** o SISLAB sobe, aplica seu próprio schema `identity` e valida a composição de DI, mas
o boot completo aborta quando o hosted service de migrations da Lumen tenta aplicar o incremento
sem o schema base. Portanto o fluxo AuthN **end-to-end contra o banco** fica bloqueado até a Lumen
publicar pacotes de migration Postgres corretos (com o `[Migration]` e a migration inicial).

**Decisão pendente** (ver relatório do E1): (a) aguardar release corrigido da Lumen; ou
(b) adotar um bootstrap de schema no lado SISLAB temporariamente (acopla a Lumen — evitar).
