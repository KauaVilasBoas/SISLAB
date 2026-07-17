import type { ReactNode } from 'react';
import { FileClock, Loader2 } from 'lucide-react';
import { Badge } from '@/shared/components/ui/badge';
import { Card, CardContent } from '@/shared/components/ui/card';
import { formatDateTime } from '@/shared/lib/format';
import type { PagedResult } from '@/shared/types/api';
import type { AuditTrailEntry } from '@/modules/controlled/types';
import {
  auditActionPresentation,
  formatAmount,
  formatDivergence,
  parseAuditPayload,
} from '@/modules/controlled/components/controlled-presentation';

interface AuditTrailProps {
  query: {
    data?: PagedResult<AuditTrailEntry>;
    isLoading: boolean;
    isError: boolean;
  };
  /** Resolves an audited stock-item id to its display name (from the controlled listing). */
  itemNameById: (id: string) => string;
}

const COLUMNS = ['Data/hora', 'Ação', 'Item', 'Quantidade', 'Divergência', 'Responsável'] as const;

/**
 * Append-only audit trail of the Controlados screen (cards [E7] #62 / #57). Renders the company's
 * controlled operations (consumption, disposal, conference) newest-first, parsing each entry's JSON
 * payload into the compliance-relevant columns (amount and, for a conference, the divergence). The item
 * name is resolved from the controlled listing; entries whose item is no longer listed fall back to a
 * short id. Presentational — loading/error/empty are standardized card states.
 */
export function AuditTrail({ query, itemNameById }: AuditTrailProps) {
  if (query.isLoading) {
    return (
      <StateCard>
        <Loader2 className="size-4 animate-spin" />
        <span className="text-sm text-muted-foreground">Carregando trilha de auditoria…</span>
      </StateCard>
    );
  }

  if (query.isError) {
    return (
      <StateCard>
        <FileClock className="size-8 text-muted-foreground" />
        <p className="text-sm text-muted-foreground">
          Não foi possível carregar a trilha de auditoria.
        </p>
      </StateCard>
    );
  }

  const entries = query.data?.items ?? [];

  if (entries.length === 0) {
    return (
      <StateCard>
        <FileClock className="size-8 text-muted-foreground" />
        <p className="text-sm text-muted-foreground">
          Nenhum registro na trilha ainda. Consumos, descartes e conferências de controlados aparecem
          aqui.
        </p>
      </StateCard>
    );
  }

  return (
    <Card>
      <div className="overflow-x-auto scrollbar-thin">
        <table className="w-full text-sm">
          <thead>
            <tr className="border-b text-left text-xs font-semibold uppercase tracking-wide text-muted-foreground">
              {COLUMNS.map((col) => (
                <th key={col} className="px-4 py-3 whitespace-nowrap">
                  {col}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {entries.map((entry) => {
              const action = auditActionPresentation(entry.action);
              const payload = parseAuditPayload(entry);
              const isConference = entry.action === 'stock-count';
              return (
                <tr key={entry.id} className="border-b transition-colors last:border-0">
                  <td className="px-4 py-3 whitespace-nowrap text-muted-foreground">
                    {formatDateTime(entry.occurredAtUtc)}
                  </td>
                  <td className="px-4 py-3">
                    <Badge variant={action.variant}>{action.label}</Badge>
                  </td>
                  <td className="px-4 py-3 font-medium">{itemNameById(entry.entityId)}</td>
                  <td className="px-4 py-3 whitespace-nowrap">
                    {formatAmount(payload.quantity, payload.unit)}
                  </td>
                  <td className="px-4 py-3 whitespace-nowrap">
                    {isConference ? (
                      <DivergenceCell divergence={payload.divergence} unit={payload.unit} />
                    ) : (
                      '—'
                    )}
                  </td>
                  <td className="px-4 py-3 text-muted-foreground">{entry.userId}</td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </Card>
  );
}

/** Renders a conference divergence, highlighting any non-zero mismatch as a compliance signal. */
function DivergenceCell({ divergence, unit }: { divergence: number | null; unit: string | null }) {
  const label = formatDivergence(divergence, unit);
  const mismatched = divergence !== null && divergence !== 0;
  return (
    <span className={mismatched ? 'font-medium text-destructive' : 'text-muted-foreground'}>
      {label}
    </span>
  );
}

function StateCard({ children }: { children: ReactNode }) {
  return (
    <Card>
      <CardContent className="flex flex-col items-center justify-center gap-3 py-16 text-center">
        {children}
      </CardContent>
    </Card>
  );
}
