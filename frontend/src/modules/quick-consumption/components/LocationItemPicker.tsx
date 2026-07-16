import { useMemo, useState } from 'react';
import { ChevronRight, PackageOpen, Search, ShieldCheck } from 'lucide-react';
import { Badge } from '@/shared/components/ui/badge';
import { Input } from '@/shared/components/ui/input';
import type { StockItemListItem } from '@/modules/inventory/types';

interface LocationItemPickerProps {
  items: StockItemListItem[];
  /** Called with the chosen item's id, which the page then loads into the baixa flow. */
  onPick: (stockItemId: string) => void;
}

/** Formats an on-hand balance with its unit, trimming trailing zeros (e.g. 12.50 → "12,5 mg"). */
function formatBalance(quantity: number, unit: string): string {
  const amount = new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 3 }).format(quantity);
  return `${amount} ${unit}`;
}

/**
 * The item chooser shown after scanning a cabinet QR (card [E7] #92): the items that location holds, with
 * a quick free-text filter, so the operator taps the exact frasco to draw from. Picking an item hands its
 * id back to the mother screen, which loads the item card and reuses the very same baixa form the item-QR
 * path uses — the two entry points converge here. Controlled drugs carry the same badge as the item card,
 * since they are the reason this per-frasco flow exists.
 */
export function LocationItemPicker({ items, onPick }: LocationItemPickerProps) {
  const [search, setSearch] = useState('');

  const filtered = useMemo(() => {
    const term = search.trim().toLowerCase();
    if (!term) return items;
    return items.filter(
      (item) =>
        item.name.toLowerCase().includes(term) ||
        (item.lotCode?.toLowerCase().includes(term) ?? false),
    );
  }, [items, search]);

  if (items.length === 0) {
    return (
      <div className="flex flex-col items-center gap-2 rounded-xl border bg-card p-8 text-center text-sm text-muted-foreground">
        <PackageOpen className="size-8 opacity-40" />
        Nenhum item neste local.
      </div>
    );
  }

  return (
    <div className="space-y-3">
      <div className="relative">
        <Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
        <Input
          type="search"
          placeholder="Buscar item ou lote…"
          className="pl-9"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          aria-label="Buscar item no local"
          autoCapitalize="off"
          autoCorrect="off"
          spellCheck={false}
        />
      </div>

      {filtered.length === 0 ? (
        <p className="px-1 py-4 text-center text-sm text-muted-foreground">
          Nenhum item corresponde à busca.
        </p>
      ) : (
        <ul className="divide-y rounded-xl border bg-card">
          {filtered.map((item) => (
            <li key={item.id}>
              <button
                type="button"
                onClick={() => onPick(item.id)}
                className="flex w-full items-center gap-3 px-4 py-3 text-left transition-colors hover:bg-muted/50"
              >
                <span className="min-w-0 flex-1">
                  <span className="flex items-center gap-2">
                    <span className="truncate text-sm font-medium">{item.name}</span>
                    {item.isControlled ? (
                      <Badge
                        variant="outline"
                        className="shrink-0 gap-1 border-status-controlled/40 bg-status-controlled/10 text-status-controlled"
                      >
                        <ShieldCheck className="size-3" />
                        Controlado
                      </Badge>
                    ) : null}
                  </span>
                  <span className="mt-0.5 block truncate text-xs text-muted-foreground">
                    Saldo {formatBalance(item.quantity, item.unit)}
                    {item.lotCode?.trim() ? ` · Lote ${item.lotCode.trim()}` : ''}
                  </span>
                </span>
                <ChevronRight className="size-4 shrink-0 text-muted-foreground" />
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
