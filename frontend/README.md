# SISLAB — Front-end

SPA cliente do SISLAB. Vive numa pasta separada do backend (`frontend/`), na raiz do
repositório, e consome a API ASP.NET (`src/Host/SISLAB.Api`).

## Stack

- **Vite** + **React 18** + **TypeScript**
- **Tailwind CSS** + **shadcn/ui** (`components.json` configurado — use `npx shadcn@latest add <componente>`)
- **Apache ECharts** (core tree-shaken) via `echarts-for-react` para gráficos
- **React Router** para navegação
- **Axios** + **TanStack Query** para chamadas à API e estado de servidor

## Arquitetura (espelha o monólito modular do backend)

```
src/
├─ app/            # composição da aplicação
│  ├─ router.tsx        # tabela de rotas (uma mother screen por módulo)
│  ├─ providers.tsx     # QueryClient (e futuros providers: tema, auth)
│  ├─ navigation.ts     # itens de menu (1 por módulo do backend)
│  └─ layout/           # AppShell, Sidebar, Topbar
├─ shared/         # transversal (equivalente ao "Shared" do backend)
│  ├─ api/http.ts       # instância Axios + unwrap do envelope ApiResult<T> + interceptor de auth
│  ├─ api/endpoints.ts  # ← CONSTANTES de endpoints, agrupadas por módulo
│  ├─ components/ui/     # primitivos shadcn (button, card, …)
│  ├─ components/        # componentes de app reutilizáveis (PageHeader, ChartCard, …)
│  ├─ lib/               # utils (cn), formatadores pt-BR, setup ECharts
│  └─ types/             # ApiResult, PagedResult, ApiError
└─ modules/        # ← espelha src/Modules/ do backend
   ├─ dashboard/    # referência: mother screen + componentes + gráfico ECharts
   ├─ inventory/  identity/  configuration/  audit/  notifications/
   └─ <cada módulo>: pages/ (mother screens)  components/ (filhos)  api/ (queries)  types.ts
```

### Padrão "mother screen"

Cada página de módulo em `modules/<m>/pages/` é o **container**: faz o data-fetching
(hooks do TanStack Query em `modules/<m>/api/`) e compõe **componentes filhos
apresentacionais** de `modules/<m>/components/`, passando dados via props. Veja
`modules/dashboard/` como referência ponta-a-ponta (KPIs + gráfico de barras ECharts).

### Endpoints

Nunca escreva um path de API direto num componente. Use `shared/api/endpoints.ts`
(`Endpoints.<modulo>.<recurso>`), que espelha os controllers do backend.

## Desenvolvimento

```bash
npm install
npm run dev      # http://localhost:5173
```

O dev server faz proxy de `/api` para o backend (`VITE_API_PROXY_TARGET`, padrão
`http://localhost:5121`), então não há CORS em desenvolvimento. Copie `.env.example`
para `.env` e ajuste conforme necessário.

- `npm run build` — type-check (`tsc -b`) + build de produção
- `npm run lint` — type-check sem emitir
