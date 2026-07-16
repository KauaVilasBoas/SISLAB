import { useEffect, useState } from 'react';
import { createPortal } from 'react-dom';
import { CalendarClock, Loader2, Pencil, Wrench, X } from 'lucide-react';
import { Button } from '@/shared/components/ui/button';
import { formatDate } from '@/shared/lib/format';
import type { ApiError } from '@/shared/types/api';
import { useToast } from '@/shared/components/ui/toast';
import { Field, Select } from '@/modules/inventory/components/form-controls';
import {
  useChangeEquipmentStatus,
  useEquipmentDetail,
} from '@/modules/inventory/api/equipment.queries';
import {
  EQUIPMENT_STATUSES,
  calibrationStatusPresentation,
  equipmentStatusPresentation,
} from '@/modules/inventory/components/equipment-presentation';
import { EquipmentCalibrationModal } from '@/modules/inventory/components/EquipmentCalibrationModal';
import { EquipmentMaintenanceModal } from '@/modules/inventory/components/EquipmentMaintenanceModal';
import type {
  EquipmentListItem,
  EquipmentStatus,
} from '@/modules/inventory/equipment.types';

interface EquipmentDetailSheetProps {
  /** The list row that was clicked; the sheet fetches the full detail behind it. */
  item: EquipmentListItem;
  onEdit: (id: string) => void;
  onClose: () => void;
}

/**
 * Right-side detail sheet for a single equipment (card [E7] #48). Fetches the full detail behind the
 * clicked row, shows the read-only attributes and hosts the aggregate actions: an inline status
 * change (each transition is its own command), plus buttons opening the calibration and maintenance
 * modals and the identification-edit form. Every action invalidates the equipment namespace so the
 * table and this sheet re-render.
 */
export function EquipmentDetailSheet({
  item,
  onEdit,
  onClose,
}: EquipmentDetailSheetProps) {
  const toast = useToast();
  const detailQuery = useEquipmentDetail(item.id);
  const changeStatus = useChangeEquipmentStatus(item.id);

  const [calibrating, setCalibrating] = useState(false);
  const [maintaining, setMaintaining] = useState(false);

  useEffect(() => {
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose();
    };
    document.addEventListener('keydown', onKeyDown);
    return () => document.removeEventListener('keydown', onKeyDown);
  }, [onClose]);

  const detail = detailQuery.data;
  const statusPresentation = equipmentStatusPresentation(item.status);
  const calibration = calibrationStatusPresentation(item.calibrationStatus);

  async function handleStatusChange(next: EquipmentStatus) {
    if (next === item.status) return;
    try {
      await changeStatus.mutateAsync({ status: next });
      toast('success', 'Status do equipamento alterado.');
    } catch (err) {
      toast('error', (err as ApiError)?.message ?? 'Não foi possível alterar o status.');
    }
  }

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
            <p className="font-mono text-sm text-muted-foreground">{item.assetTag}</p>
          </div>
          <button
            type="button"
            onClick={onClose}
            aria-label="Fechar"
            className="rounded-md p-1 text-muted-foreground transition-colors hover:bg-accent hover:text-accent-foreground"
          >
            <X className="size-4" />
          </button>
        </header>

        <div className="flex-1 space-y-6 overflow-y-auto p-5 scrollbar-thin">
          <div className="flex flex-wrap items-center gap-2">
            <span className={statusPresentation.className}>
              {statusPresentation.label}
            </span>
            <span className={calibration.className}>Calibração: {calibration.label}</span>
          </div>

          <dl className="grid grid-cols-2 gap-x-4 gap-y-3 text-sm">
            <DetailRow label="Marca" value={detail?.manufacturer ?? '—'} />
            <DetailRow label="Modelo" value={detail?.model ?? '—'} />
            <DetailRow label="Local" value={item.storageLocationName ?? '—'} />
            <DetailRow
              label="Últ. calibração"
              value={formatDate(detail?.lastCalibrationDate)}
            />
            <DetailRow
              label="Próx. calibração"
              value={formatDate(item.nextCalibrationDate)}
            />
            <DetailRow
              label="Últ. manutenção"
              value={formatDate(detail?.lastMaintenanceDate)}
            />
          </dl>

          {detailQuery.isLoading ? (
            <p className="flex items-center gap-2 text-sm text-muted-foreground">
              <Loader2 className="size-4 animate-spin" />
              Carregando detalhes…
            </p>
          ) : null}

          <section className="space-y-3">
            <h3 className="text-sm font-medium">Status operacional</h3>
            <Field label="Alterar status" htmlFor="equipment-status">
              <Select
                id="equipment-status"
                value={item.status}
                disabled={changeStatus.isPending}
                onChange={(e) => handleStatusChange(e.target.value as EquipmentStatus)}
              >
                {EQUIPMENT_STATUSES.map((status) => (
                  <option key={status} value={status}>
                    {equipmentStatusPresentation(status).label}
                  </option>
                ))}
              </Select>
            </Field>
          </section>

          <section className="space-y-3">
            <h3 className="text-sm font-medium">Ações</h3>
            <div className="grid grid-cols-2 gap-2">
              <Button variant="outline" size="sm" onClick={() => onEdit(item.id)}>
                <Pencil className="size-3.5" />
                Editar
              </Button>
              <Button
                variant="outline"
                size="sm"
                disabled={!detail}
                onClick={() => setCalibrating(true)}
              >
                <CalendarClock className="size-3.5" />
                Calibração
              </Button>
              <Button
                variant="outline"
                size="sm"
                disabled={!detail}
                onClick={() => setMaintaining(true)}
              >
                <Wrench className="size-3.5" />
                Manutenção
              </Button>
            </div>
          </section>
        </div>
      </aside>

      {calibrating && detail ? (
        <EquipmentCalibrationModal
          equipment={detail}
          onClose={() => setCalibrating(false)}
        />
      ) : null}

      {maintaining && detail ? (
        <EquipmentMaintenanceModal
          equipment={detail}
          onClose={() => setMaintaining(false)}
        />
      ) : null}
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
