import { useMemo, useState, type ReactNode } from 'react';
import { Printer, QrCode, Search } from 'lucide-react';
import { PageHeader } from '@/shared/components/PageHeader';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import { cn } from '@/shared/lib/utils';
import { Select } from '@/modules/inventory/components/form-controls';
import { useStorageLocations } from '@/modules/inventory/api/inventory.queries';
import type { StockItemFilters } from '@/modules/inventory/types';
import { useLabelStockItems, LABEL_ITEMS_LIMIT } from '@/modules/labels/api/labels.queries';
import { LabelSelectionList } from '@/modules/labels/components/LabelSelectionList';
import { LabelSheet } from '@/modules/labels/components/LabelSheet';
import {
  toItemLabel,
  toLocationLabel,
  type LabelSpec,
} from '@/modules/labels/lib/label-model';

type LabelSource = 'items' | 'locations';

/**
 * QR-labels mother screen (card [E7] #92).
 *
 * Lets an operator pick stock items and/or storage locations and print a sheet of QR stickers to glue on
 * the cabinets — the physical half of the quick-consumption flow (#63): scanning an item sticker opens
 * its baixa, scanning a location sticker opens that cabinet's item list. Every code is built through the
 * shared `sislab:` grammar, so generation here and scanning there can never drift.
 *
 * The screen owns one selection set keyed by kind-namespaced ids, so items and locations coexist on the
 * same sheet: the operator can tick a few items, switch the source to Locais, tick some cabinets, and
 * print both at once. The right pane is the live preview *and* the print surface — `window.print()` with
 * the print isolation in index.css renders only that sheet, no server-side PDF.
 */
export function LabelsPage() {
  const [source, setSource] = useState<LabelSource>('items');
  const [filters, setFilters] = useState<StockItemFilters>({});
  const [selected, setSelected] = useState<Set<string>>(new Set());

  const locations = useStorageLocations();
  const items = useLabelStockItems(filters);

  // Candidate labels for each source, built once per fetch. Locations are always fully loaded (small
  // set); items honour the search/location filter so an operator can print, say, "só a geladeira".
  const itemLabels = useMemo<LabelSpec[]>(
    () => (items.data?.items ?? []).map(toItemLabel),
    [items.data],
  );
  const locationLabels = useMemo<LabelSpec[]>(
    () => (locations.data ?? []).map(toLocationLabel),
    [locations.data],
  );

  const visibleCandidates = source === 'items' ? itemLabels : locationLabels;

  // The sheet is every selected label across BOTH sources, so switching the source never drops a pick.
  const selectedLabels = useMemo<LabelSpec[]>(
    () => [...itemLabels, ...locationLabels].filter((label) => selected.has(label.selectionKey)),
    [itemLabels, locationLabels, selected],
  );

  const overItemLimit = (items.data?.totalCount ?? 0) > LABEL_ITEMS_LIMIT;

  function patchFilters(patch: Partial<StockItemFilters>) {
    setFilters((prev) => ({ ...prev, ...patch }));
  }

  function toggle(selectionKey: string) {
    setSelected((prev) => {
      const next = new Set(prev);
      if (next.has(selectionKey)) next.delete(selectionKey);
      else next.add(selectionKey);
      return next;
    });
  }

  function selectAllVisible() {
    setSelected((prev) => {
      const next = new Set(prev);
      visibleCandidates.forEach((c) => next.add(c.selectionKey));
      return next;
    });
  }

  function clearVisible() {
    setSelected((prev) => {
      const next = new Set(prev);
      visibleCandidates.forEach((c) => next.delete(c.selectionKey));
      return next;
    });
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title="Etiquetas QR"
        description="Gere e imprima etiquetas QR de itens e locais para o registro rápido no armário."
        actions={
          <Button onClick={() => window.print()} disabled={selectedLabels.length === 0}>
            <Printer className="size-4" />
            Imprimir ({selectedLabels.length})
          </Button>
        }
      />

      <div className="grid gap-6 lg:grid-cols-[minmax(0,26rem)_1fr]">
        {/* Left: source picker + filters + selection list */}
        <div className="space-y-4">
          <div role="tablist" aria-label="Origem das etiquetas" className="flex gap-1 border-b">
            <SourceTab active={source === 'items'} onClick={() => setSource('items')}>
              Itens
            </SourceTab>
            <SourceTab active={source === 'locations'} onClick={() => setSource('locations')}>
              Locais
            </SourceTab>
          </div>

          {source === 'items' ? (
            <div className="flex flex-col gap-3 sm:flex-row">
              <div className="relative flex-1">
                <Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
                <Input
                  type="search"
                  placeholder="Buscar por nome, lote ou marca…"
                  className="pl-9"
                  value={filters.search ?? ''}
                  onChange={(e) => patchFilters({ search: e.target.value })}
                  aria-label="Buscar itens"
                />
              </div>
              <Select
                className="sm:w-48"
                value={filters.storageLocationId ?? ''}
                onChange={(e) => patchFilters({ storageLocationId: e.target.value || undefined })}
                aria-label="Filtrar por local"
              >
                <option value="">Todos os locais</option>
                {(locations.data ?? []).map((l) => (
                  <option key={l.id} value={l.id}>
                    {l.name}
                  </option>
                ))}
              </Select>
            </div>
          ) : null}

          {overItemLimit ? (
            <p className="rounded-md border border-status-warning/40 bg-status-warning/10 px-3 py-2 text-xs text-foreground">
              Mostrando os primeiros {LABEL_ITEMS_LIMIT} itens. Refine a busca ou o local para etiquetar o
              restante.
            </p>
          ) : null}

          <LabelSelectionList
            candidates={visibleCandidates}
            selected={selected}
            onToggle={toggle}
            onSelectAll={selectAllVisible}
            onClear={clearVisible}
            isLoading={source === 'items' ? items.isLoading : locations.isLoading}
            emptyMessage={
              source === 'items'
                ? 'Nenhum item encontrado para o filtro atual.'
                : 'Nenhum local cadastrado.'
            }
          />
        </div>

        {/* Right: printable sheet (also the live preview) */}
        <div className="space-y-3">
          <div className="flex items-center gap-2 text-sm text-muted-foreground print:hidden">
            <QrCode className="size-4" />
            {selectedLabels.length === 0
              ? 'Selecione itens ou locais para montar a folha de etiquetas.'
              : `${selectedLabels.length} etiqueta${selectedLabels.length === 1 ? '' : 's'} na folha`}
          </div>

          {selectedLabels.length === 0 ? (
            <div className="flex min-h-64 flex-col items-center justify-center gap-2 rounded-lg border border-dashed p-8 text-center text-sm text-muted-foreground print:hidden">
              <QrCode className="size-8 opacity-40" />
              A folha aparece aqui conforme você seleciona.
            </div>
          ) : (
            <LabelSheet labels={selectedLabels} />
          )}
        </div>
      </div>
    </div>
  );
}

/** A single tab in the source toggle (Itens / Locais); the active tab carries the primary underline. */
function SourceTab({
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
