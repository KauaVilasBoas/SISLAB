import { Search } from 'lucide-react';
import { Input } from '@/shared/components/ui/input';
import { Select } from '@/modules/inventory/components/form-controls';
import {
  useItemCategories,
  useStorageLocations,
} from '@/modules/inventory/api/inventory.queries';
import type { StockItemFilters } from '@/modules/inventory/types';

interface StockItemsFilterBarProps {
  filters: StockItemFilters;
  onChange: (patch: Partial<StockItemFilters>) => void;
}

/**
 * Filter bar for the inventory table: free-text search (name/lot/brand), storage-location and
 * category selectors. Emits filter patches to the page, which owns the filter state and resets the
 * page number on change. The location/category catalogues are the same ones the create form uses.
 */
export function StockItemsFilterBar({ filters, onChange }: StockItemsFilterBarProps) {
  const locations = useStorageLocations();
  const categories = useItemCategories();

  return (
    <div className="flex flex-col gap-3 sm:flex-row sm:items-center">
      <div className="relative flex-1">
        <Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
        <Input
          type="search"
          placeholder="Buscar por nome, lote ou marca…"
          className="pl-9"
          value={filters.search ?? ''}
          onChange={(e) => onChange({ search: e.target.value })}
          aria-label="Buscar itens"
        />
      </div>

      <Select
        className="sm:w-56"
        value={filters.storageLocationId ?? ''}
        onChange={(e) => onChange({ storageLocationId: e.target.value || undefined })}
        aria-label="Filtrar por local"
      >
        <option value="">Todos os locais</option>
        {(locations.data ?? []).map((l) => (
          <option key={l.id} value={l.id}>
            {l.name}
          </option>
        ))}
      </Select>

      <Select
        className="sm:w-56"
        value={filters.category ?? ''}
        onChange={(e) => onChange({ category: e.target.value || undefined })}
        aria-label="Filtrar por categoria"
      >
        <option value="">Todas as categorias</option>
        {(categories.data ?? []).map((c) => (
          <option key={c.id} value={c.name}>
            {c.name}
          </option>
        ))}
      </Select>
    </div>
  );
}
