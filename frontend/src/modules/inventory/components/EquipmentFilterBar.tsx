import { Search } from 'lucide-react';
import { Input } from '@/shared/components/ui/input';
import { Select } from '@/modules/inventory/components/form-controls';
import { useStorageLocations } from '@/modules/inventory/api/inventory.queries';
import {
  CALIBRATION_STATUSES,
  calibrationStatusPresentation,
} from '@/modules/inventory/components/equipment-presentation';
import type {
  CalibrationStatus,
  EquipmentFilters,
} from '@/modules/inventory/equipment.types';

interface EquipmentFilterBarProps {
  filters: EquipmentFilters;
  onChange: (patch: Partial<EquipmentFilters>) => void;
}

/**
 * Filter bar for the equipment table (card [E7] #48): free-text search (name/asset tag), derived
 * calibration-status and storage-location selectors. Emits filter patches to the page, which owns the
 * filter state and resets the page number on change.
 */
export function EquipmentFilterBar({ filters, onChange }: EquipmentFilterBarProps) {
  const locations = useStorageLocations();

  return (
    <div className="flex flex-col gap-3 sm:flex-row sm:items-center">
      <div className="relative flex-1">
        <Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
        <Input
          type="search"
          placeholder="Buscar por nome ou tombamento…"
          className="pl-9"
          value={filters.search ?? ''}
          onChange={(e) => onChange({ search: e.target.value })}
          aria-label="Buscar equipamentos"
        />
      </div>

      <Select
        className="sm:w-56"
        value={filters.status ?? ''}
        onChange={(e) =>
          onChange({
            status: (e.target.value || undefined) as CalibrationStatus | undefined,
          })
        }
        aria-label="Filtrar por calibração"
      >
        <option value="">Toda calibração</option>
        {CALIBRATION_STATUSES.map((status) => (
          <option key={status} value={status}>
            {calibrationStatusPresentation(status).label}
          </option>
        ))}
      </Select>

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
    </div>
  );
}
