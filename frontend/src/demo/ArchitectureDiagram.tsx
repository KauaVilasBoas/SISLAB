import { type ReactNode } from 'react';
import {
  Database,
  Layers,
  MonitorSmartphone,
  Package,
  Server,
  type LucideIcon,
} from 'lucide-react';
import { cn } from '@/shared/lib/utils';

const BOUNDED_CONTEXTS = [
  'Identity',
  'Configuration',
  'Inventory',
  'Notifications',
  'Audit',
  'Experiments',
  'Agenda',
];

type Tone = 'neutral' | 'host' | 'module' | 'shared' | 'data';

const TONE_STYLES: Record<Tone, string> = {
  neutral: 'border-border bg-muted/50',
  host: 'border-status-info/40 bg-status-info/10',
  module: 'border-status-ok/40 bg-status-ok/10',
  shared: 'border-status-controlled/40 bg-status-controlled/10',
  data: 'border-status-info/60 bg-status-info/15',
};

const TONE_ICON: Record<Tone, string> = {
  neutral: 'text-muted-foreground',
  host: 'text-status-info',
  module: 'text-status-ok',
  shared: 'text-status-controlled',
  data: 'text-status-info',
};

export function ArchitectureDiagram() {
  return (
    <figure className="space-y-2">
      <div className="flex flex-col items-stretch rounded-xl border bg-background/60 p-3 sm:p-4">
        <DiagramNode
          tone="neutral"
          icon={MonitorSmartphone}
          title="React SPA"
          subtitle="Vite · TypeScript · Tailwind · ECharts"
          badge="você está aqui"
        />

        <Connector label="HTTPS · REST · cookie httpOnly + CSRF" />

        <DiagramNode
          tone="host"
          icon={Server}
          title="SISLAB.Api: Composition Root"
          subtitle="Exception → CorrelationId → Rate limit → AuthN → Tenant → CSRF → AuthZ"
          aside="Jobs · outbox + alertas"
        />

        <Connector />

        <DiagramNode
          tone="module"
          icon={Layers}
          title="7 bounded contexts"
          subtitle="cada um com Domain · Application · Infrastructure · Contracts, conversam só por Contracts"
          aside="IAM: Lumen (NuGet)"
        >
          <ul className="flex flex-wrap gap-1.5">
            {BOUNDED_CONTEXTS.map((context) => (
              <li
                key={context}
                className="rounded-full border border-status-ok/30 bg-background/70 px-2 py-0.5 text-[11px] font-medium"
              >
                {context}
              </li>
            ))}
          </ul>
        </DiagramNode>

        <Connector />

        <DiagramNode
          tone="shared"
          icon={Package}
          title="SharedKernel · Infrastructure"
          subtitle="AggregateRoot · ValueObject · mediator próprio (Validation → Transaction → Logging) · UnitOfWork · Outbox"
        />

        <Connector label="EF Core na escrita · Dapper na leitura" />

        <DiagramNode
          tone="data"
          icon={Database}
          title="PostgreSQL"
          subtitle="um schema por contexto, cada um com seu outbox_messages · toda linha carrega company_id"
        />
      </div>

      <figcaption className="text-center text-[11px] text-muted-foreground">
        Visão simplificada. O diagrama completo está no README do repositório.
      </figcaption>
    </figure>
  );
}

interface DiagramNodeProps {
  tone: Tone;
  icon: LucideIcon;
  title: string;
  subtitle: string;
  aside?: string;
  badge?: string;
  children?: ReactNode;
}

function DiagramNode({
  tone,
  icon: Icon,
  title,
  subtitle,
  aside,
  badge,
  children,
}: DiagramNodeProps) {
  return (
    <div className={cn('space-y-1.5 rounded-lg border px-3 py-2.5', TONE_STYLES[tone])}>
      <div className="flex items-center gap-2">
        <Icon className={cn('size-4 shrink-0', TONE_ICON[tone])} aria-hidden />
        <p className="flex-1 text-sm font-semibold leading-none">{title}</p>
        {badge && (
          <span className="shrink-0 rounded-full bg-primary px-2 py-0.5 text-[10px] font-semibold text-primary-foreground">
            {badge}
          </span>
        )}
        {aside && (
          <span className="hidden shrink-0 rounded-full border border-border/60 bg-background/70 px-2 py-0.5 text-[10px] font-medium text-muted-foreground sm:block">
            {aside}
          </span>
        )}
      </div>
      <p className="text-[11px] leading-snug text-muted-foreground">{subtitle}</p>
      {children}
    </div>
  );
}

function Connector({ label }: { label?: string }) {
  return (
    <div className="flex items-center gap-2 py-1 pl-5" aria-hidden>
      <span className="h-5 w-px bg-border" />
      {label && <span className="text-[10px] text-muted-foreground">{label}</span>}
    </div>
  );
}
