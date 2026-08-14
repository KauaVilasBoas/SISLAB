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

/**
 * Superfície OPT-IN "Como foi construído": o conteúdo técnico que antes abria logo na entrada da demo.
 * Fica atrás de um link discreto (no modal de boas-vindas e na DemoRibbon), servindo dev e recrutador
 * sem impor jargão a quem toca o laboratório. Reaproveita o Modal padrão e o ArchitectureDiagram.
 */
export function HowItsBuiltDialog({ onClose }: { onClose: () => void }) {
  return (
    <Modal
      open
      onClose={onClose}
      size="xl"
      title="Como o SISLAB foi construído"
      description="Uma visão da engenharia por trás do produto: arquitetura, padrões e decisões de projeto."
      footer={
        <>
          <Button variant="outline" asChild>
            <a href={SISLAB_REPO} target="_blank" rel="noreferrer">
              <ExternalLink aria-hidden />
              Ver o código
            </a>
          </Button>
          <Button onClick={onClose}>Fechar</Button>
        </>
      }
    >
      <div className="space-y-6">
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
