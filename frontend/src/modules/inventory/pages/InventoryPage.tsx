import { useMemo, useState, type ReactNode } from 'react';
import { Plus } from 'lucide-react';
import { PageHeader } from '@/shared/components/PageHeader';
import { Button } from '@/shared/components/ui/button';
import { cn } from '@/shared/lib/utils';
import { useStockItems } from '@/modules/inventory/api/inventory.queries';
import { StockItemsFilterBar } from '@/modules/inventory/components/StockItemsFilterBar';
import { StockItemsTable } from '@/modules/inventory/components/StockItemsTable';
import { StockItemDetailSheet } from '@/modules/inventory/components/StockItemDetailSheet';
import { StockItemFormModal } from '@/modules/inventory/components/StockItemFormModal';
import { StockMovementsTab } from '@/modules/inventory/components/StockMovementsTab';
import type { StockItemFilters, StockItemListItem } from '@/modules/inventory/types';

type InventoryTab = 'stock' | 'movements';

/**
 * Inventory master screen (cards [E7] #46 / #47). Two tabs share one selected item: "Estoque" is the
 * filterable, paginated stock-item table with a create form and a right-side detail sheet that hosts
 * the item's movement actions; "Movimentações" is the read-only movement ledger of the item selected
 * on the Estoque tab. Owns the filter/page and selection state; the stock-item query key includes the
 * filter and page, so React Query caches each combination and the movement mutations (invalidating the
 * whole stock-item namespace, which the movements key nests under) refresh both the list and the ledger.
 */
export function InventoryPage() {
  const [tab, setTab] = useState<InventoryTab>('stock');
  const [filters, setFilters] = useState<StockItemFilters>({});
  const [page, setPage] = useState(1);
  const [creating, setCreating] = useState(false);
  const [selected, setSelected] = useState<StockItemListItem | null>(null);

  const query = useStockItems(filters, page);

  function patchFilters(patch: Partial<StockItemFilters>) {
    setFilters((prev) => ({ ...prev, ...patch }));
    setPage(1);
  }

  const totalPages = query.data?.totalPages ?? 0;
  const totalCount = query.data?.totalCount ?? 0;

  // Keep the selected item in sync with the freshly fetched page so the sheet shows the new balance
  // after a movement invalidates the list (the row identity is the item id).
  const liveSelected = useMemo(() => {
    if (!selected) return null;
    return query.data?.items.find((i) => i.id === selected.id) ?? selected;
  }, [selected, query.data]);

  return (
    <div className="space-y-6">
      <PageHeader
        title="Estoque"
        description="Itens de estoque do laboratório: saldo, validade, lote e movimentações."
        actions={
          <Button onClick={() => setCreating(true)}>
            <Plus className="size-4" />
            Novo item
          </Button>
        }
      />

      <div role="tablist" aria-label="Seções do estoque" className="flex gap-1 border-b">
        <TabButton active={tab === 'stock'} onClick={() => setTab('stock')}>
          Estoque
        </TabButton>
        <TabButton active={tab === 'movements'} onClick={() => setTab('movements')}>
          Movimentações
        </TabButton>
      </div>

      {tab === 'stock' ? (
        <>
          <StockItemsFilterBar filters={filters} onChange={patchFilters} />

          <StockItemsTable query={query} onSelect={setSelected} />

          {totalPages > 1 ? (
            <div className="flex items-center justify-between gap-4 text-sm text-muted-foreground">
              <span>
                {totalCount} {totalCount === 1 ? 'item' : 'itens'} · página {page} de{' '}
                {totalPages}
              </span>
              <div className="flex gap-2">
                <Button
                  variant="outline"
                  size="sm"
                  disabled={page <= 1 || query.isFetching}
                  onClick={() => setPage((p) => Math.max(1, p - 1))}
                >
                  Anterior
                </Button>
                <Button
                  variant="outline"
                  size="sm"
                  disabled={page >= totalPages || query.isFetching}
                  onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                >
                  Próxima
                </Button>
              </div>
            </div>
          ) : null}
        </>
      ) : (
        <StockMovementsTab item={liveSelected} />
      )}

      {creating ? <StockItemFormModal onClose={() => setCreating(false)} /> : null}

      {tab === 'stock' && liveSelected ? (
        <StockItemDetailSheet item={liveSelected} onClose={() => setSelected(null)} />
      ) : null}
    </div>
  );
}

/** A single tab in the inventory tab bar; the active tab carries the primary underline. */
function TabButton({
  active,
  onClick,
  children,
}: {
  active: boolean;
  onClick: () => void;
  children: ReactNode;
}) {
  return (
    <button
      type="button"
      role="tab"
      aria-selected={active}
      onClick={onClick}
      className={cn(
        '-mb-px border-b-2 px-4 py-2 text-sm font-medium transition-colors',
        active
          ? 'border-primary text-foreground'
          : 'border-transparent text-muted-foreground hover:text-foreground',
      )}
    >
      {children}
    </button>
  );
}
