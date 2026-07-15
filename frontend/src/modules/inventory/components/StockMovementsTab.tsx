import { useEffect, useState, type ReactNode } from 'react';
import { History, Loader2, PackageX } from 'lucide-react';
import { Badge } from '@/shared/components/ui/badge';
import { Button } from '@/shared/components/ui/button';
import { Card, CardContent } from '@/shared/components/ui/card';
import { Input } from '@/shared/components/ui/input';
import { useToast } from '@/shared/components/ui/toast';
import type { ApiError } from '@/shared/types/api';
import { formatDate } from '@/shared/lib/format';
import { Field, Select } from '@/modules/inventory/components/form-controls';
import { useStockMovements } from '@/modules/inventory/api/inventory.queries';
import {
  formatQuantity,
  MOVEMENT_TYPES,
  movementTypePresentation,
} from '@/modules/inventory/components/stock-presentation';
import type {
  StockItemListItem,
  StockMovementListItem,
  StockMovementsFilter,
  StockMovementType,
} from '@/modules/inventory/types';

interface StockMovementsTabProps {
  /** The item selected on the Estoque tab; when null the tab prompts the operator to pick one. */
  item: StockItemListItem | null;
}

const COLUMNS = ['Data', 'Tipo', 'Quantidade', 'Observações', 'Responsável'] as const;

/**
 * Movements tab (card [E7] #47): the paginated ledger of a single stock item — entries, consumptions,
 * transfers and disposals — most recent first, with a movement-type and date-range filter. Owns only
 * the filter/page state; the item to inspect is chosen on the Estoque tab and passed in. Every query
 * error is surfaced as a toast (no inline error text); loading/empty are standardized card states.
 */
export function StockMovementsTab({ item }: StockMovementsTabProps) {
  const [filters, setFilters] = useState<StockMovementsFilter>({});
  const [page, setPage] = useState(1);
  const toast = useToast();

  const query = useStockMovements(item?.id, filters, page);

  useEffect(() => {
    if (query.isError) {
      toast(
        'error',
        (query.error as unknown as ApiError)?.message ??
          'Não foi possível carregar as movimentações.',
      );
    }
  }, [query.isError, query.error, toast]);

  function patchFilters(patch: Partial<StockMovementsFilter>) {
    setFilters((prev) => ({ ...prev, ...patch }));
    setPage(1);
  }

  if (!item) {
    return (
      <StateCard>
        <History className="size-8 text-muted-foreground" />
        <p className="text-sm text-muted-foreground">
          Selecione um item na aba Estoque para ver o histórico de movimentações.
        </p>
      </StateCard>
    );
  }

  const totalPages = query.data?.totalPages ?? 0;
  const totalCount = query.data?.totalCount ?? 0;
  const movements = query.data?.items ?? [];

  return (
    <div className="space-y-4">
      <div className="flex flex-col gap-1">
        <h2 className="text-sm font-semibold">
          Movimentações de <span className="font-bold">{item.name}</span>
        </h2>
        <p className="text-xs text-muted-foreground">
          Histórico de entradas, consumos, transferências e descartes deste item.
        </p>
      </div>

      <div className="flex flex-col gap-3 sm:flex-row sm:items-end">
        <Field label="Tipo" htmlFor="movement-type" className="sm:w-52">
          <Select
            id="movement-type"
            value={filters.type ?? ''}
            onChange={(e) =>
              patchFilters({ type: (e.target.value || undefined) as StockMovementType | undefined })
            }
          >
            <option value="">Todos os tipos</option>
            {MOVEMENT_TYPES.map((type) => (
              <option key={type} value={type}>
                {movementTypePresentation(type).label}
              </option>
            ))}
          </Select>
        </Field>

        <Field label="De" htmlFor="movement-from" className="sm:w-40">
          <Input
            id="movement-from"
            type="date"
            value={filters.from ?? ''}
            max={filters.to || undefined}
            onChange={(e) => patchFilters({ from: e.target.value || undefined })}
          />
        </Field>

        <Field label="Até" htmlFor="movement-to" className="sm:w-40">
          <Input
            id="movement-to"
            type="date"
            value={filters.to ?? ''}
            min={filters.from || undefined}
            onChange={(e) => patchFilters({ to: e.target.value || undefined })}
          />
        </Field>
      </div>

      <MovementsTable query={query} movements={movements} />

      {totalPages > 1 ? (
        <div className="flex items-center justify-between gap-4 text-sm text-muted-foreground">
          <span>
            {totalCount} {totalCount === 1 ? 'movimentação' : 'movimentações'} · página{' '}
            {page} de {totalPages}
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
  );
}

function MovementsTable({
  query,
  movements,
}: {
  query: { isLoading: boolean; isError: boolean };
  movements: StockMovementListItem[];
}) {
  if (query.isLoading) {
    return (
      <StateCard>
        <Loader2 className="size-4 animate-spin" />
        <span className="text-sm text-muted-foreground">Carregando movimentações…</span>
      </StateCard>
    );
  }

  if (query.isError) {
    return (
      <StateCard>
        <PackageX className="size-8 text-muted-foreground" />
        <p className="text-sm text-muted-foreground">
          Não foi possível carregar as movimentações.
        </p>
      </StateCard>
    );
  }

  if (movements.length === 0) {
    return (
      <StateCard>
        <PackageX className="size-8 text-muted-foreground" />
        <p className="text-sm text-muted-foreground">
          Nenhuma movimentação encontrada para os filtros selecionados.
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
            {movements.map((movement) => {
              const presentation = movementTypePresentation(movement.type);
              return (
                <tr key={movement.id} className="border-b transition-colors last:border-0">
                  <td className="px-4 py-3 whitespace-nowrap">
                    {formatDate(`${movement.occurredAt}T00:00:00`)}
                  </td>
                  <td className="px-4 py-3">
                    <Badge variant={presentation.variant}>{presentation.label}</Badge>
                  </td>
                  <td className="px-4 py-3 whitespace-nowrap font-medium">
                    {formatQuantity(movement.quantity, movement.unit)}
                  </td>
                  <td className="px-4 py-3 text-muted-foreground">
                    {movement.notes ?? '—'}
                  </td>
                  <td className="px-4 py-3 text-muted-foreground">
                    {movement.performedBy ?? '—'}
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
