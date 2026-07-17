import { useMemo, useState } from 'react';
import { Plus } from 'lucide-react';
import { PageHeader } from '@/shared/components/PageHeader';
import { Button } from '@/shared/components/ui/button';
import { RequirePermission } from '@/modules/auth/PermissionsProvider';
import { Permissions } from '@/modules/auth/permissions';
import {
  useEquipmentDetail,
  useEquipmentList,
} from '@/modules/inventory/api/equipment.queries';
import { EquipmentFilterBar } from '@/modules/inventory/components/EquipmentFilterBar';
import { EquipmentTable } from '@/modules/inventory/components/EquipmentTable';
import { EquipmentFormModal } from '@/modules/inventory/components/EquipmentFormModal';
import { EquipmentDetailSheet } from '@/modules/inventory/components/EquipmentDetailSheet';
import type {
  EquipmentFilters,
  EquipmentListItem,
} from '@/modules/inventory/equipment.types';

/**
 * Equipment master screen (card [E7] #48). A filterable, paginated equipment table with summary pills
 * (total + overdue calibrations), a create/edit form and a right-side detail sheet hosting the
 * aggregate actions (status change, calibration, maintenance). The overdue-calibration count comes from
 * a dedicated, filtered listing query so the pill reflects the whole tenant, not just the current page.
 */
export function EquipmentPage() {
  const [filters, setFilters] = useState<EquipmentFilters>({});
  const [page, setPage] = useState(1);
  const [creating, setCreating] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [selected, setSelected] = useState<EquipmentListItem | null>(null);

  const query = useEquipmentList(filters, page);
  // Tenant-wide overdue count for the summary pill (page 1 is enough — we only read totalCount).
  const overdueQuery = useEquipmentList({ status: 'Overdue' }, 1);
  const editingQuery = useEquipmentDetail(editingId ?? undefined);

  function patchFilters(patch: Partial<EquipmentFilters>) {
    setFilters((prev) => ({ ...prev, ...patch }));
    setPage(1);
  }

  const totalPages = query.data?.totalPages ?? 0;
  const totalCount = query.data?.totalCount ?? 0;
  const overdueCount = overdueQuery.data?.totalCount ?? 0;

  // Keep the selected row in sync with the freshly fetched page so the sheet reflects a status change.
  const liveSelected = useMemo(() => {
    if (!selected) return null;
    return query.data?.items.find((i) => i.id === selected.id) ?? selected;
  }, [selected, query.data]);

  const editingEquipment = editingId ? editingQuery.data : undefined;

  return (
    <div className="space-y-6">
      <PageHeader
        title="Equipamentos"
        description="Cadastro de equipamentos, status operacional, calibração e manutenções."
        actions={
          <RequirePermission code={Permissions.equipment.register}>
            <Button onClick={() => setCreating(true)}>
              <Plus className="size-4" />
              Novo equipamento
            </Button>
          </RequirePermission>
        }
      />

      <div className="flex flex-wrap gap-2 text-sm">
        <SummaryPill
          label={`${totalCount} ${totalCount === 1 ? 'equipamento' : 'equipamentos'}`}
        />
        <SummaryPill
          label={`${overdueCount} ${overdueCount === 1 ? 'calibração atrasada' : 'calibrações atrasadas'}`}
          tone={overdueCount > 0 ? 'warning' : 'muted'}
        />
      </div>

      <EquipmentFilterBar filters={filters} onChange={patchFilters} />

      <EquipmentTable query={query} onSelect={setSelected} />

      {totalPages > 1 ? (
        <div className="flex items-center justify-between gap-4 text-sm text-muted-foreground">
          <span>
            {totalCount} {totalCount === 1 ? 'equipamento' : 'equipamentos'} · página{' '}
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

      {creating ? <EquipmentFormModal onClose={() => setCreating(false)} /> : null}

      {editingEquipment ? (
        <EquipmentFormModal
          equipment={editingEquipment}
          onClose={() => setEditingId(null)}
        />
      ) : null}

      {liveSelected ? (
        <EquipmentDetailSheet
          item={liveSelected}
          onEdit={(id) => {
            setSelected(null);
            setEditingId(id);
          }}
          onClose={() => setSelected(null)}
        />
      ) : null}
    </div>
  );
}

/** A single summary pill in the equipment header (total / overdue calibrations). */
function SummaryPill({
  label,
  tone = 'muted',
}: {
  label: string;
  tone?: 'muted' | 'warning';
}) {
  return (
    <span
      className={
        tone === 'warning'
          ? 'inline-flex items-center rounded-full bg-amber-100 px-3 py-1 font-medium text-amber-800 dark:bg-amber-950 dark:text-amber-200'
          : 'inline-flex items-center rounded-full bg-muted px-3 py-1 font-medium text-muted-foreground'
      }
    >
      {label}
    </span>
  );
}
