import { History, Loader2, PackageX } from 'lucide-react';
import { Badge } from '@/shared/components/ui/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/shared/components/ui/card';
import { formatRelativeTime } from '@/shared/lib/format';
import { useRecentMovements } from '@/modules/inventory/api/inventory.queries';
import {
  formatQuantity,
  movementTypePresentation,
} from '@/modules/inventory/components/stock-presentation';
import type { RecentMovementItem, StockItemListItem } from '@/modules/inventory/types';

interface RecentMovementsPanelProps {
  /** Optional row-click handler — selecting a movement opens the parent item's detail sheet. */
  onSelectItem?: (item: Pick<StockItemListItem, 'id' | 'name'>) => void;
}

/** How many recent movements the panel shows; small by design (a page-level activity feed). */
const RECENT_TOP = 8;

/**
 * Cross-item "Atividade recente" panel (card [E7] #47): the active company's latest movements across
 * every item — entry, consumption, transfer, disposal — most recent first. Complements the per-item
 * ledger (Movimentações tab): this one is not pinned to a selected item, so it answers "what moved in
 * the lab lately?" at a glance. Each row can be clicked to open its item's detail sheet.
 */
export function RecentMovementsPanel({ onSelectItem }: RecentMovementsPanelProps) {
  const query = useRecentMovements(RECENT_TOP);
  const movements = query.data ?? [];

  return (
    <Card>
      <CardHeader className="flex-row items-center gap-2 space-y-0">
        <History className="size-4 text-muted-foreground" />
        <CardTitle className="text-sm font-semibold">Atividade recente</CardTitle>
      </CardHeader>
      <CardContent className="px-0 pb-2">
        <PanelBody query={query} movements={movements} onSelectItem={onSelectItem} />
      </CardContent>
    </Card>
  );
}

function PanelBody({
  query,
  movements,
  onSelectItem,
}: {
  query: { isLoading: boolean; isError: boolean };
  movements: RecentMovementItem[];
  onSelectItem?: (item: Pick<StockItemListItem, 'id' | 'name'>) => void;
}) {
  if (query.isLoading) {
    return (
      <PanelState>
        <Loader2 className="size-4 animate-spin" />
        <span className="text-sm text-muted-foreground">Carregando movimentações…</span>
      </PanelState>
    );
  }

  if (query.isError) {
    return (
      <PanelState>
        <PackageX className="size-8 text-muted-foreground" />
        <p className="text-sm text-muted-foreground">
          Não foi possível carregar a atividade recente.
        </p>
      </PanelState>
    );
  }

  if (movements.length === 0) {
    return (
      <PanelState>
        <PackageX className="size-8 text-muted-foreground" />
        <p className="text-sm text-muted-foreground">Nenhuma movimentação registrada ainda.</p>
      </PanelState>
    );
  }

  return (
    <ul className="divide-y">
      {movements.map((movement) => (
        <RecentMovementRow key={movement.id} movement={movement} onSelectItem={onSelectItem} />
      ))}
    </ul>
  );
}

function RecentMovementRow({
  movement,
  onSelectItem,
}: {
  movement: RecentMovementItem;
  onSelectItem?: (item: Pick<StockItemListItem, 'id' | 'name'>) => void;
}) {
  const presentation = movementTypePresentation(movement.type);
  const select = onSelectItem
    ? () => onSelectItem({ id: movement.stockItemId, name: movement.stockItemName })
    : undefined;

  return (
    <li>
      <button
        type="button"
        onClick={select}
        disabled={!select}
        className="flex w-full items-center gap-3 px-6 py-3 text-left transition-colors enabled:hover:bg-muted/50 disabled:cursor-default"
      >
        <Badge variant={presentation.variant} className="shrink-0">
          {presentation.label}
        </Badge>
        <div className="min-w-0 flex-1">
          <p className="truncate text-sm font-medium">{movement.stockItemName}</p>
          <p className="text-xs text-muted-foreground">
            {formatQuantity(movement.quantity, movement.unit)}
          </p>
        </div>
        <span className="shrink-0 text-xs text-muted-foreground">
          {movement.occurredOn ? formatRelativeTime(`${movement.occurredOn}T00:00:00`) : '—'}
        </span>
      </button>
    </li>
  );
}

function PanelState({ children }: { children: React.ReactNode }) {
  return (
    <div className="flex flex-col items-center justify-center gap-3 px-6 py-10 text-center">
      {children}
    </div>
  );
}
