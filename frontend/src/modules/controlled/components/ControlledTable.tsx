import type { ReactNode } from 'react';
import { ClipboardCheck, Loader2, ScrollText, ShieldOff } from 'lucide-react';
import { Card, CardContent } from '@/shared/components/ui/card';
import { Badge } from '@/shared/components/ui/badge';
import { Button } from '@/shared/components/ui/button';
import type { PagedResult } from '@/shared/types/api';
import {
  containerStateLabel,
  expiryStatusPresentation,
  formatExpiry,
  formatQuantity,
} from '@/modules/inventory/components/stock-presentation';
import type { ControlledItem } from '@/modules/controlled/types';

interface ControlledTableProps {
  query: {
    data?: PagedResult<ControlledItem>;
    isLoading: boolean;
    isError: boolean;
  };
  /** Opens the conference modal for a controlled item. */
  onConference: (item: ControlledItem) => void;
  /** Filters the audit trail to a single item ("log" action). */
  onAudit: (item: ControlledItem) => void;
}

const COLUMNS = ['Fármaco', 'Lote', 'Saldo', 'Validade', 'Estado', ''] as const;

/**
 * Controlled-substances table (card [E7] #62). Renders each controlled StockItem with its per-bottle
 * balance, lot, validity (colored) and container state, plus two per-row actions: "Conferir" (opens the
 * conference modal) and "log" (narrows the audit trail to that item). Presentational — loading/error/empty
 * are standardized card states and every action is delegated up.
 */
export function ControlledTable({ query, onConference, onAudit }: ControlledTableProps) {
  if (query.isLoading) {
    return (
      <StateCard>
        <Loader2 className="size-4 animate-spin" />
        <span className="text-sm text-muted-foreground">Carregando controlados…</span>
      </StateCard>
    );
  }

  if (query.isError) {
    return (
      <StateCard>
        <ShieldOff className="size-8 text-muted-foreground" />
        <p className="text-sm text-muted-foreground">
          Não foi possível carregar os fármacos controlados.
        </p>
      </StateCard>
    );
  }

  const items = query.data?.items ?? [];

  if (items.length === 0) {
    return (
      <StateCard>
        <ShieldOff className="size-8 text-muted-foreground" />
        <p className="text-sm text-muted-foreground">
          Nenhum fármaco controlado cadastrado. Marque um item como controlado no Estoque.
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
              {COLUMNS.map((col, index) => (
                <th key={col || index} className="px-4 py-3 whitespace-nowrap">
                  {col}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {items.map((item) => {
              const expiry = expiryStatusPresentation(item.expiryStatus);
              return (
                <tr key={item.id} className="border-b transition-colors last:border-0 hover:bg-accent/50">
                  <td className="px-4 py-3">
                    <div className="flex items-center gap-2 font-medium">
                      {item.name}
                      <Badge variant="outline" className="uppercase">
                        CTRL
                      </Badge>
                    </div>
                    {item.storageLocationName ? (
                      <span className="text-xs text-muted-foreground">
                        {item.storageLocationName}
                      </span>
                    ) : null}
                  </td>
                  <td className="px-4 py-3 text-muted-foreground">{item.lotCode ?? '—'}</td>
                  <td className="px-4 py-3 whitespace-nowrap">
                    <span className={item.isBelowMinimum ? 'font-medium text-destructive' : 'font-medium'}>
                      {formatQuantity(item.quantity, item.unit)}
                    </span>
                  </td>
                  <td className="px-4 py-3 whitespace-nowrap">
                    <div className="flex items-center gap-2">
                      <span>{formatExpiry(item.expiryYear, item.expiryMonth)}</span>
                      <Badge variant={expiry.variant}>{expiry.label}</Badge>
                    </div>
                  </td>
                  <td className="px-4 py-3 text-muted-foreground">
                    {containerStateLabel(item.containerState)}
                  </td>
                  <td className="px-4 py-3">
                    <div className="flex items-center justify-end gap-2">
                      <Button
                        variant="outline"
                        size="sm"
                        onClick={() => onConference(item)}
                        aria-label={`Registrar conferência de ${item.name}`}
                      >
                        <ClipboardCheck className="size-4" />
                        Conferir
                      </Button>
                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={() => onAudit(item)}
                        aria-label={`Ver trilha de auditoria de ${item.name}`}
                      >
                        <ScrollText className="size-4" />
                        log
                      </Button>
                    </div>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </Card>
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
