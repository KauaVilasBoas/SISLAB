# SISLAB

Sistema de gestão para laboratório, substituindo o controle atual em planilhas de Excel por uma
aplicação web multi-tenant. Cada empresa (`companyId`) opera isolada, com seus próprios itens de
estoque, equipamentos, parceiros e — em fase futura — experimentos.

> **Status:** em planejamento/estruturação. O backlog completo está no Trello
> ([board SISLAB](https://trello.com/b/C8qhOb3j/sislab)), organizado por épicos (E0–E9) e sprints.

## Arquitetura

Monólito modular em .NET, separado por _bounded contexts_, seguindo **DDD + CQRS + Clean Code**,
desenhado para uma eventual evolução para microsserviços sem reescrita.

- **Presentation** — SPA **React** (Vite + TypeScript + Tailwind + [shadcn/ui](https://ui.shadcn.com/),
  com [Apache ECharts](https://echarts.apache.org/) para dashboards) consumindo uma **API .NET**.
- **Módulos de domínio** — cada um com `Domain`, `Application`, `Infrastructure` e `Contracts`.
  Um módulo só enxerga o outro pela camada `Contracts` (interface + DTO próprio), **nunca**
  referenciando o `Domain` alheio (regra reforçada por testes de arquitetura).
- **SharedKernel** — Core compartilhado: `Constants`, `Util`, `Exceptions` e abstrações de domínio.
- **Infrastructure** — `DbContext` base, `UnitOfWork`, Outbox e EventBus.
- **Jobs** (`SISLAB.Jobs`) — worker de alertas (validade, estoque baixo) e processamento de Outbox.

### Persistência e consistência

- **Write-side:** EF Core (agregados, invariantes) sobre **PostgreSQL**.
- **Read-side:** Dapper com SQL puro (query + handler + result no mesmo arquivo).
- **Eventos:** estratégia híbrida — Outbox/eventual para efeitos colaterais; handler síncrono
  na mesma transação (com rollback) apenas para invariantes de negócio genuínas.
- **Multi-tenancy:** `companyId` aplicado em profundidade (JWT → command → _global query filter_
  no EF → `WHERE company_id` obrigatório no Dapper).

## Stack

| Camada | Tecnologia |
|---|---|
| Back-end | .NET, EF Core, Dapper, PostgreSQL |
| Front-end | React, TypeScript, Vite, Tailwind, shadcn/ui, ECharts |
| Infra | AWS Free Tier (Elastic Beanstalk, RDS PostgreSQL, S3/CloudFront), Terraform |
| CI/CD | GitHub Actions |

## Roadmap (MVP — módulo de Estoque)

Caminho crítico: **E0 → E1 → E2 → E3 → E4 → E7** (com E8 de infra em paralelo desde cedo).

1. **E0** — Esqueleto modular
2. **E1** — Identity & Tenancy
3. **E2** — Plataforma CQRS + Eventos
4. **E3** — Inventory (write): itens, locais, movimentações, equipamentos, parceiros
5. **E4** — Inventory (leitura) & relatórios
6. **E5** — Contracts & integração entre módulos
7. **E6** — Jobs (alertas, Outbox)
8. **E7** — SPA React
9. **E8** — Infra AWS & CI/CD
10. **E9** — Observabilidade & Segurança

## Desenvolvimento

Pré-requisitos: .NET SDK, Node.js, PostgreSQL (local ou container), Terraform.

```bash
dotnet build SISLAB.sln
```

> Dados sensíveis do laboratório (`doc.md`, planilhas, exports) são intencionalmente
> mantidos fora do versionamento — ver `.gitignore`.
