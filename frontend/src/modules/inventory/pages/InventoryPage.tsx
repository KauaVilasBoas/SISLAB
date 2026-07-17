import { useMemo, useState, type ReactNode } from 'react';
import { Plus, Warehouse } from 'lucide-react';
import { PageHeader } from '@/shared/components/PageHeader';
import { Button } from '@/shared/components/ui/button';
import {
  RequireAnyPermission,
  RequirePermission,
  usePermissions,
} from '@/modules/auth/PermissionsProvider';
import { Permissions } from '@/modules/auth/permissions';
import { cn } from '@/shared/lib/utils';
import { useStockItems } from '@/modules/inventory/api/inventory.queries';
import { StockItemsFilterBar } from '@/modules/inventory/components/StockItemsFilterBar';
import { StockItemsTable } from '@/modules/inventory/components/StockItemsTable';
import { LocationsSidebar } from '@/modules/inventory/components/LocationsSidebar';
import { RecentMovementsPanel } from '@/modules/inventory/components/RecentMovementsPanel';
import { StockItemDetailSheet } from '@/modules/inventory/components/StockItemDetailSheet';
import { StockItemFormModal } from '@/modules/inventory/components/StockItemFormModal';
import { StorageLocationModal } from '@/modules/inventory/components/StorageLocationModal';
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
  const [editing, setEditing] = useState(false);
  const [managingLocations, setManagingLocations] = useState(false);
  const [selected, setSelected] = useState<StockItemListItem | null>(null);
  const { hasPermission } = usePermissions();

  // The ledger read endpoint is gated by Stock.ListStockMovements; without it the whole "Movimentações" tab
  // is hidden (its query would 403). The user is kept on the Estoque tab in that case.
  const canReadMovements = hasPermission(Permissions.stock.listMovements);

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

  // Opens the detail sheet for an item picked from the recent-activity panel — only when the item is on
  // the current page (the sheet needs its full row). A movement on another page is a no-op for now.
  function selectItemById(stockItemId: string) {
    const item = query.data?.items.find((i) => i.id === stockItemId);
    if (item) setSelected(item);
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title="Estoque"
        description="Itens de estoque do laboratório: saldo, validade, lote e movimentações."
        actions={
          <>
            <RequireAnyPermission
              codes={[
                Permissions.storageLocations.register,
                Permissions.storageLocations.update,
                Permissions.storageLocations.changeStatus,
              ]}
            >
              <Button variant="outline" onClick={() => setManagingLocations(true)}>
                <Warehouse className="size-4" />
                Gerenciar locais
              </Button>
            </RequireAnyPermission>
            <RequirePermission code={Permissions.stock.registerItem}>
              <Button onClick={() => setCreating(true)}>
                <Plus className="size-4" />
                Novo item
              </Button>
            </RequirePermission>
          </>
        }
      />

      <div role="tablist" aria-label="Seções do estoque" className="flex gap-1 border-b">
        <TabButton active={tab === 'stock'} onClick={() => setTab('stock')}>
          Estoque
        </TabButton>
        {canReadMovements ? (
          <TabButton active={tab === 'movements'} onClick={() => setTab('movements')}>
            Movimentações
          </TabButton>
        ) : null}
      </div>

      {tab === 'stock' || !canReadMovements ? (
        <div className="grid gap-6 lg:grid-cols-[16rem_minmax(0,1fr)]">
          <div className="space-y-6">
            <LocationsSidebar
              selectedLocationId={filters.storageLocationId}
              onSelect={(storageLocationId) => patchFilters({ storageLocationId })}
            />

            <RecentMovementsPanel onSelectItem={(item) => selectItemById(item.id)} />
          </div>

          <div className="min-w-0 space-y-6">
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
          </div>
        </div>
      ) : (
        <StockMovementsTab item={liveSelected} />
      )}

      {creating ? <StockItemFormModal onClose={() => setCreating(false)} /> : null}

      {managingLocations ? (
        <StorageLocationModal onClose={() => setManagingLocations(false)} />
      ) : null}

      {editing && liveSelected ? (
        <StockItemFormModal item={liveSelected} onClose={() => setEditing(false)} />
      ) : null}

      {tab === 'stock' && liveSelected ? (
        <StockItemDetailSheet
          item={liveSelected}
          onEdit={() => setEditing(true)}
          onClose={() => setSelected(null)}
        />
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
