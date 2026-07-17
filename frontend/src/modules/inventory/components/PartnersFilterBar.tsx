import { Search } from 'lucide-react';
import { Input } from '@/shared/components/ui/input';
import { Select } from '@/modules/inventory/components/form-controls';
import {
  PARTNER_TYPES,
  partnerTypePresentation,
} from '@/modules/inventory/components/partner-presentation';
import type { PartnerFilters, PartnerType } from '@/modules/inventory/partner.types';

interface PartnersFilterBarProps {
  filters: PartnerFilters;
  onChange: (patch: Partial<PartnerFilters>) => void;
}

/**
 * Filter bar for the partners grid (card [E7] #48): free-text search (name/document) and a type
 * selector. Emits filter patches to the page, which owns the filter state and resets the page number
 * on change.
 */
export function PartnersFilterBar({ filters, onChange }: PartnersFilterBarProps) {
  return (
    <div className="flex flex-col gap-3 sm:flex-row sm:items-center">
      <div className="relative flex-1">
        <Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
        <Input
          type="search"
          placeholder="Buscar por nome ou CNPJ…"
          className="pl-9"
          value={filters.search ?? ''}
          onChange={(e) => onChange({ search: e.target.value })}
          aria-label="Buscar parceiros"
        />
      </div>

      <Select
        className="sm:w-56"
        value={filters.type ?? ''}
        onChange={(e) =>
          onChange({ type: (e.target.value || undefined) as PartnerType | undefined })
        }
        aria-label="Filtrar por tipo"
      >
        <option value="">Todos os tipos</option>
        {PARTNER_TYPES.map((type) => (
          <option key={type} value={type}>
            {partnerTypePresentation(type).label}
          </option>
        ))}
      </Select>
    </div>
  );
}
