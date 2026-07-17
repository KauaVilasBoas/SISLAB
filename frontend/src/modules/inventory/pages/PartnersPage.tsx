import { useState } from 'react';
import { Plus } from 'lucide-react';
import { PageHeader } from '@/shared/components/PageHeader';
import { Button } from '@/shared/components/ui/button';
import { usePartnerList } from '@/modules/inventory/api/partner.queries';
import { PartnersFilterBar } from '@/modules/inventory/components/PartnersFilterBar';
import { PartnersGrid } from '@/modules/inventory/components/PartnersGrid';
import { PartnerFormModal } from '@/modules/inventory/components/PartnerFormModal';
import type { PartnerFilters, PartnerListItem } from '@/modules/inventory/partner.types';

/**
 * Partners master screen (card [E7] #48). A filterable, paginated grid of partner cards with a
 * create/edit form. Owns the filter/page state and the partner being edited; the grid hosts the
 * activate/deactivate actions inline. Suppliers/partners are the origin of stock entries and the
 * senders of samples (GDA compounds) for testing — the descriptive "amostras" text lives on the
 * partner's description until the Experiments module owns that entity.
 */
export function PartnersPage() {
  const [filters, setFilters] = useState<PartnerFilters>({});
  const [page, setPage] = useState(1);
  const [creating, setCreating] = useState(false);
  const [editing, setEditing] = useState<PartnerListItem | null>(null);

  const query = usePartnerList(filters, page);

  function patchFilters(patch: Partial<PartnerFilters>) {
    setFilters((prev) => ({ ...prev, ...patch }));
    setPage(1);
  }

  const totalPages = query.data?.totalPages ?? 0;
  const totalCount = query.data?.totalCount ?? 0;

  return (
    <div className="space-y-6">
      <PageHeader
        title="Parceiros"
        description="Fornecedores e parceiros que enviam substâncias para teste."
        actions={
          <Button onClick={() => setCreating(true)}>
            <Plus className="size-4" />
            Novo parceiro
          </Button>
        }
      />

      <PartnersFilterBar filters={filters} onChange={patchFilters} />

      <PartnersGrid query={query} onEdit={setEditing} />

      {totalPages > 1 ? (
        <div className="flex items-center justify-between gap-4 text-sm text-muted-foreground">
          <span>
            {totalCount} {totalCount === 1 ? 'parceiro' : 'parceiros'} · página {page} de{' '}
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

      {creating ? <PartnerFormModal onClose={() => setCreating(false)} /> : null}

      {editing ? (
        <PartnerFormModal partner={editing} onClose={() => setEditing(null)} />
      ) : null}
    </div>
  );
}
