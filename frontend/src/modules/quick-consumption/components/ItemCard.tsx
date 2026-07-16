import { ShieldCheck } from 'lucide-react';
import { Badge } from '@/shared/components/ui/badge';
import type { StockItemDetail } from '@/modules/quick-consumption/types';

interface ItemCardProps {
  item: StockItemDetail;
}

/** Formats an on-hand balance with its unit, trimming trailing zeros (e.g. 12.50 → "12,5 mg"). */
function formatBalance(quantity: number, unit: string): string {
  const amount = new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 3 }).format(quantity);
  return `${amount} ${unit}`;
}

/**
 * Dumb card for the scanned item (card [E7] #63): name, category, current lot and on-hand balance —
 * the operator's confirmation that the right label was read before registering a consumption. Controlled
 * drugs get a visible badge, since they are the reason this "tintim por tintim" flow exists.
 */
export function ItemCard({ item }: ItemCardProps) {
  return (
    <div className="rounded-xl border bg-card p-4 shadow-sm">
      <div className="flex items-start justify-between gap-2">
        <div className="min-w-0">
          <h2 className="truncate text-base font-semibold leading-tight">{item.name}</h2>
          <p className="truncate text-xs text-muted-foreground">{item.category}</p>
        </div>
        {item.isControlled && (
          <Badge
            variant="outline"
            className="shrink-0 gap-1 border-status-controlled/40 bg-status-controlled/10 text-status-controlled"
          >
            <ShieldCheck className="size-3" />
            Controlado
          </Badge>
        )}
      </div>

      <dl className="mt-3 grid grid-cols-2 gap-3 text-sm">
        <div>
          <dt className="text-xs text-muted-foreground">Saldo atual</dt>
          <dd className="font-semibold">{formatBalance(item.quantity, item.unit)}</dd>
        </div>
        <div>
          <dt className="text-xs text-muted-foreground">Lote</dt>
          <dd className="font-medium">{item.lot?.trim() || '—'}</dd>
        </div>
      </dl>
    </div>
  );
}
