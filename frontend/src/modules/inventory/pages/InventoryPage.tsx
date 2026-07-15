import { useMemo, useState } from 'react';
import { Plus } from 'lucide-react';
import { PageHeader } from '@/shared/components/PageHeader';
import { Button } from '@/shared/components/ui/button';
import { useStockItems } from '@/modules/inventory/api/inventory.queries';
import { StockItemsFilterBar } from '@/modules/inventory/components/StockItemsFilterBar';
import { StockItemsTable } from '@/modules/inventory/components/StockItemsTable';
import { StockItemDetailSheet } from '@/modules/inventory/components/StockItemDetailSheet';
import { StockItemFormModal } from '@/modules/inventory/components/StockItemFormModal';
import type { StockItemFilters, StockItemListItem } from '@/modules/inventory/types';

/**
 * Inventory master screen (card [E7] #46): the filterable, paginated stock-item table with a create
 * form and a right-side detail sheet that hosts the item's stock movements. Owns the filter and page
 * state; the query key includes both, so React Query caches each filter/page combination and the
 * mutations (invalidating the whole stock-item namespace) refresh the visible page.
 */
export function InventoryPage() {
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

      {creating ? <StockItemFormModal onClose={() => setCreating(false)} /> : null}

      {liveSelected ? (
        <StockItemDetailSheet item={liveSelected} onClose={() => setSelected(null)} />
      ) : null}
    </div>
  );
}
