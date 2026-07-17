import { useEffect, useState } from 'react';
import { createPortal } from 'react-dom';
import {
  ArrowRightLeft,
  PackagePlus,
  Pencil,
  ShieldCheck,
  Trash2,
  TrendingDown,
  X,
} from 'lucide-react';
import { Badge } from '@/shared/components/ui/badge';
import { Button } from '@/shared/components/ui/button';
import type { StockItemListItem } from '@/modules/inventory/types';
import {
  containerStateLabel,
  expiryStatusPresentation,
  formatExpiry,
  formatQuantity,
} from '@/modules/inventory/components/stock-presentation';
import { StockMovementForms } from '@/modules/inventory/components/StockMovementForms';

interface StockItemDetailSheetProps {
  item: StockItemListItem;
  onEdit: () => void;
  onClose: () => void;
}

type MovementKind = 'entry' | 'consumption' | 'transfer' | 'disposal';

const MOVEMENTS: { kind: MovementKind; label: string; icon: typeof PackagePlus }[] = [
  { kind: 'entry', label: 'Entrada', icon: PackagePlus },
  { kind: 'consumption', label: 'Consumo', icon: TrendingDown },
  { kind: 'transfer', label: 'Transferir', icon: ArrowRightLeft },
  { kind: 'disposal', label: 'Descartar', icon: Trash2 },
];

/**
 * Right-side detail sheet for a single stock item (card [E7] #46). Shows the read-only attributes and
 * hosts the two ways to change the item: "Editar" opens the metadata form (name, category, location,
 * minimum, brand, application), and the movement actions (entry, consumption, transfer, disposal) change
 * the balance. Each movement action expands an inline form; a successful movement invalidates the item
 * list so the sheet's caller re-renders with the new balance.
 */
export function StockItemDetailSheet({ item, onEdit, onClose }: StockItemDetailSheetProps) {
  const [movement, setMovement] = useState<MovementKind | null>(null);
  const expiry = expiryStatusPresentation(item.expiryStatus);

  useEffect(() => {
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose();
    };
    document.addEventListener('keydown', onKeyDown);
    return () => document.removeEventListener('keydown', onKeyDown);
  }, [onClose]);

  return createPortal(
    <div
      className="fixed inset-0 z-50 flex justify-end bg-black/50"
      onMouseDown={onClose}
    >
      <aside
        role="dialog"
        aria-modal="true"
        aria-label={`Detalhes de ${item.name}`}
        className="flex h-full w-full max-w-md flex-col border-l bg-card text-card-foreground shadow-lg"
        onMouseDown={(e) => e.stopPropagation()}
      >
        <header className="flex items-start justify-between gap-4 border-b p-5">
          <div className="min-w-0 space-y-1">
            <h2 className="truncate text-lg font-semibold tracking-tight">{item.name}</h2>
            <p className="text-sm text-muted-foreground">{item.category}</p>
          </div>
          <div className="flex shrink-0 items-center gap-1">
            <Button variant="outline" size="sm" onClick={onEdit}>
              <Pencil className="size-3.5" />
              Editar
            </Button>
            <button
              type="button"
              onClick={onClose}
              aria-label="Fechar"
              className="rounded-md p-1 text-muted-foreground transition-colors hover:bg-accent hover:text-accent-foreground"
            >
              <X className="size-4" />
            </button>
          </div>
        </header>

        <div className="flex-1 space-y-6 overflow-y-auto p-5 scrollbar-thin">
          <div className="flex flex-wrap gap-2">
            <Badge variant={expiry.variant}>{expiry.label}</Badge>
            <Badge variant="outline">{containerStateLabel(item.containerState)}</Badge>
            {item.isBelowMinimum ? <Badge>Abaixo do mínimo</Badge> : null}
            {item.isControlled ? (
              <Badge variant="secondary">
                <ShieldCheck className="size-3" />
                Controlado
              </Badge>
            ) : null}
          </div>

          <dl className="grid grid-cols-2 gap-x-4 gap-y-3 text-sm">
            <DetailRow
              label="Quantidade"
              value={formatQuantity(item.quantity, item.unit)}
            />
            <DetailRow
              label="Mínimo"
              value={formatQuantity(item.minimumQuantity, item.minimumUnit)}
            />
            <DetailRow label="Marca" value={item.brand ?? '—'} />
            <DetailRow label="Lote" value={item.lotCode ?? '—'} />
            <DetailRow
              label="Validade"
              value={formatExpiry(item.expiryYear, item.expiryMonth)}
            />
            <DetailRow label="Local" value={item.storageLocationName ?? '—'} />
            <DetailRow
              label="Aplicação"
              value={item.application ?? '—'}
              className="col-span-2"
            />
          </dl>

          <section className="space-y-3">
            <h3 className="text-sm font-medium">Movimentações</h3>
            <div className="grid grid-cols-2 gap-2">
              {MOVEMENTS.map(({ kind, label, icon: Icon }) => (
                <Button
                  key={kind}
                  variant={movement === kind ? 'default' : 'outline'}
                  size="sm"
                  onClick={() => setMovement((prev) => (prev === kind ? null : kind))}
                >
                  <Icon className="size-3.5" />
                  {label}
                </Button>
              ))}
            </div>

            {movement ? (
              <StockMovementForms
                kind={movement}
                item={item}
                onDone={() => setMovement(null)}
              />
            ) : null}
          </section>
        </div>
      </aside>
    </div>,
    document.body,
  );
}

function DetailRow({
  label,
  value,
  className,
}: {
  label: string;
  value: string;
  className?: string;
}) {
  return (
    <div className={className}>
      <dt className="text-xs uppercase tracking-wide text-muted-foreground">{label}</dt>
      <dd className="mt-0.5 font-medium">{value}</dd>
    </div>
  );
}
