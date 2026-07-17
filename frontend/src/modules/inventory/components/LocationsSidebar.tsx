import { AlertTriangle, Loader2, MapPin, Warehouse } from 'lucide-react';
import { Card } from '@/shared/components/ui/card';
import { Badge } from '@/shared/components/ui/badge';
import { cn } from '@/shared/lib/utils';
import { useStorageLocations } from '@/modules/inventory/api/inventory.queries';

interface LocationsSidebarProps {
  /** The location currently narrowing the item table, or undefined for "all locations". */
  selectedLocationId?: string;
  /** Selects a location as the table filter; passing undefined clears it ("all locations"). */
  onSelect: (locationId: string | undefined) => void;
}

/**
 * Storage-locations side column of the Estoque tab (card [E7] #46). Lists the active company's locations
 * with their item count and — the point of the card — a badge counting the expired items stored there, so
 * the operator sees at a glance where perished stock is piling up. Selecting a location narrows the item
 * table; the active row is the same location the table is filtered by, so the two stay in sync.
 *
 * It reuses the same GetLocationsSummary read model the location filter drives from (item/expired counts
 * and the critical flag come straight off it), so no extra endpoint is needed.
 */
export function LocationsSidebar({
  selectedLocationId,
  onSelect,
}: LocationsSidebarProps) {
  const locations = useStorageLocations();
  const items = locations.data ?? [];

  return (
    <Card className="flex flex-col overflow-hidden">
      <header className="flex items-center gap-2 border-b px-4 py-3 text-sm font-semibold">
        <Warehouse className="size-4 text-muted-foreground" />
        Locais
      </header>

      {locations.isLoading ? (
        <div className="flex items-center justify-center gap-2 py-10 text-sm text-muted-foreground">
          <Loader2 className="size-4 animate-spin" />
          Carregando locais…
        </div>
      ) : locations.isError ? (
        <p className="px-4 py-10 text-center text-sm text-destructive">
          Não foi possível carregar os locais.
        </p>
      ) : items.length === 0 ? (
        <p className="px-4 py-10 text-center text-sm text-muted-foreground">
          Nenhum local cadastrado.
        </p>
      ) : (
        <ul className="divide-y">
          <li>
            <LocationRow
              label="Todos os locais"
              active={!selectedLocationId}
              onClick={() => onSelect(undefined)}
            />
          </li>
          {items.map((location) => (
            <li key={location.id}>
              <LocationRow
                label={location.name}
                itemCount={location.itemCount}
                expiredCount={location.expiredItemCount}
                critical={location.isCritical}
                active={selectedLocationId === location.id}
                onClick={() => onSelect(location.id)}
              />
            </li>
          ))}
        </ul>
      )}
    </Card>
  );
}

function LocationRow({
  label,
  itemCount,
  expiredCount = 0,
  critical = false,
  active,
  onClick,
}: {
  label: string;
  itemCount?: number;
  expiredCount?: number;
  critical?: boolean;
  active: boolean;
  onClick: () => void;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      aria-current={active}
      className={cn(
        'flex w-full items-center gap-2 px-4 py-2.5 text-left text-sm transition-colors',
        active ? 'bg-accent text-accent-foreground' : 'hover:bg-accent/50',
      )}
    >
      <MapPin
        className={cn(
          'size-4 shrink-0',
          critical ? 'text-destructive' : 'text-muted-foreground',
        )}
      />
      <span className="min-w-0 flex-1 truncate font-medium">{label}</span>
      {itemCount !== undefined ? (
        <span className="shrink-0 text-xs text-muted-foreground">{itemCount}</span>
      ) : null}
      {expiredCount > 0 ? (
        <Badge variant="default" className="shrink-0 gap-1">
          <AlertTriangle className="size-3" />
          {expiredCount}
        </Badge>
      ) : null}
    </button>
  );
}
