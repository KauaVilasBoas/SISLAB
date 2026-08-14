import {
  Building2,
  Database,
  ExternalLink,
  KeyRound,
  Server,
  ShieldCheck,
  Workflow,
  type LucideIcon,
} from 'lucide-react';
import { Button } from '@/shared/components/ui/button';
import { Modal } from '@/shared/components/ui/modal';
import { ArchitectureDiagram } from '@/demo/ArchitectureDiagram';

const SISLAB_REPO = 'https://github.com/KauaVilasBoas/SISLAB';
const LUMEN_REPO = 'https://github.com/KauaVilasBoas/Lumen';

interface Highlight {
  icon: LucideIcon;
  title: string;
  detail: string;
}

const BACKEND_HIGHLIGHTS: Highlight[] = [
  {
    icon: Server,
    title: '.NET 8 · monólito modular',
    detail: '7 bounded contexts isolados, validados por 69 regras de ArchUnit',
  },
  {
    icon: Workflow,
    title: 'DDD + CQRS',
    detail: 'domínio rico, mediator próprio e Outbox transacional para eventos',
  },
  {
    icon: Database,
    title: 'EF Core + Dapper',
    detail: 'escrita pelos aggregates, leitura paginada em SQL dedicado',
  },
  {
    icon: Building2,
    title: 'Multi-tenant',
    detail: 'isolamento por company_id em toda consulta, do cookie ao SQL',
  },
  {
    icon: ShieldCheck,
    title: '~1.100 testes',
    detail: 'unidade, integração e arquitetura rodando a cada build',
  },
  {
    icon: KeyRound,
    title: 'IAM próprio',
    detail: 'autenticação e permissões via Lumen, publicada por mim no NuGet',
  },
];

export function DemoIntroDialog({ onClose }: { onClose: () => void }) {
  return (
    <Modal
      open
      onClose={onClose}
      size="xl"
      title="Bem-vindo à demonstração do SISLAB"
      description="Sistema de gestão para laboratórios de pesquisa: estoque, controlados, equipamentos, agenda e experimentos."
      footer={
        <>
          <Button variant="outline" asChild>
            <a href={SISLAB_REPO} target="_blank" rel="noreferrer">
              <ExternalLink aria-hidden />
              Ver o código
            </a>
          </Button>
          <Button onClick={onClose}>Explorar o sistema</Button>
        </>
      }
    >
      <div className="space-y-6">
        <p className="text-sm leading-relaxed text-muted-foreground">
          Esta é uma <strong className="text-foreground">vitrine somente leitura</strong>:
          não há servidor por trás dela. As chamadas{' '}
          <code className="rounded bg-muted px-1 py-0.5 font-mono text-xs text-foreground">
            /api/*
          </code>{' '}
          são interceptadas no navegador e respondidas com dados fictícios: nada é
          gravado e nenhum dado real de laboratório é exposto. Explore à vontade, todas as
          telas estão abertas.
        </p>

        <section className="space-y-3">
          <h3 className="text-sm font-semibold">O peso do projeto está no backend</h3>
          <ul className="grid gap-2 sm:grid-cols-2">
            {BACKEND_HIGHLIGHTS.map(({ icon: Icon, title, detail }) => (
              <li
                key={title}
                className="flex items-start gap-2.5 rounded-lg border bg-muted/40 px-3 py-2.5"
              >
                <Icon className="mt-0.5 size-4 shrink-0 text-primary" aria-hidden />
                <span className="min-w-0">
                  <span className="block text-[13px] font-medium leading-tight">
                    {title}
                  </span>
                  <span className="block text-[11px] leading-snug text-muted-foreground">
                    {detail}
                  </span>
                </span>
              </li>
            ))}
          </ul>
        </section>

        <section className="space-y-3">
          <h3 className="text-sm font-semibold">Como as peças se encaixam</h3>
          <ArchitectureDiagram />
        </section>

        <section className="flex flex-col gap-2 rounded-lg border border-dashed p-3 sm:flex-row sm:items-center sm:justify-between">
          <p className="text-xs text-muted-foreground">
            Código aberto: backend, frontend e infraestrutura como código.
          </p>
          <div className="flex flex-wrap gap-3 text-xs font-medium">
            <RepoLink href={SISLAB_REPO} label="SISLAB" />
            <RepoLink href={LUMEN_REPO} label="Lumen (biblioteca de IAM)" />
          </div>
        </section>
      </div>
    </Modal>
  );
}

function RepoLink({ href, label }: { href: string; label: string }) {
  return (
    <a
      href={href}
      target="_blank"
      rel="noreferrer"
      className="inline-flex items-center gap-1.5 text-primary underline-offset-4 hover:underline"
    >
      <ExternalLink className="size-3.5" aria-hidden />
      {label}
    </a>
  );
}
