import { useState, type FormEvent } from 'react';
import { Loader2 } from 'lucide-react';
import { Modal } from '@/shared/components/ui/modal';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import type { ApiError } from '@/shared/types/api';
import { useToast } from '@/shared/components/ui/toast';
import { Field, Select } from '@/modules/inventory/components/form-controls';
import { useRecordEquipmentMaintenance } from '@/modules/inventory/api/equipment.queries';
import {
  MAINTENANCE_TYPES,
  maintenanceTypeLabel,
} from '@/modules/inventory/components/equipment-presentation';
import type {
  EquipmentDetail,
  MaintenanceType,
  RecordEquipmentMaintenanceRequest,
} from '@/modules/inventory/equipment.types';

interface EquipmentMaintenanceModalProps {
  equipment: EquipmentDetail;
  onClose: () => void;
}

/** Today as an ISO "YYYY-MM-DD" string, the default maintenance date. */
function today(): string {
  return new Date().toISOString().slice(0, 10);
}

/**
 * Logs a maintenance event against an equipment (card [E7] #48): a dated, typed entry with optional
 * notes. On success it invalidates the equipment namespace so the detail's "última manutenção"
 * refreshes.
 */
export function EquipmentMaintenanceModal({
  equipment,
  onClose,
}: EquipmentMaintenanceModalProps) {
  const toast = useToast();
  const record = useRecordEquipmentMaintenance(equipment.id);

  const [date, setDate] = useState(today());
  const [type, setType] = useState<MaintenanceType>('Preventive');
  const [notes, setNotes] = useState('');

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const body: RecordEquipmentMaintenanceRequest = {
      date,
      type,
      notes: notes.trim() === '' ? null : notes.trim(),
    };
    try {
      await record.mutateAsync(body);
      toast('success', 'Manutenção registrada.');
      onClose();
    } catch (err) {
      toast(
        'error',
        (err as ApiError)?.message ?? 'Não foi possível registrar a manutenção.',
      );
    }
  }

  return (
    <Modal
      open
      onClose={onClose}
      title="Registrar manutenção"
      description="Registre uma manutenção preventiva, corretiva ou de calibração."
      footer={
        <>
          <Button variant="outline" onClick={onClose} disabled={record.isPending}>
            Cancelar
          </Button>
          <Button
            type="submit"
            form="equipment-maintenance-form"
            disabled={record.isPending}
          >
            {record.isPending && <Loader2 className="size-4 animate-spin" />}
            Registrar
          </Button>
        </>
      }
    >
      <form
        id="equipment-maintenance-form"
        className="grid grid-cols-1 gap-4"
        onSubmit={handleSubmit}
        noValidate
      >
        <Field label="Data" htmlFor="maintenance-date">
          <Input
            id="maintenance-date"
            type="date"
            value={date}
            max={today()}
            onChange={(e) => setDate(e.target.value)}
            required
          />
        </Field>

        <Field label="Tipo" htmlFor="maintenance-type">
          <Select
            id="maintenance-type"
            value={type}
            onChange={(e) => setType(e.target.value as MaintenanceType)}
          >
            {MAINTENANCE_TYPES.map((t) => (
              <option key={t} value={t}>
                {maintenanceTypeLabel(t)}
              </option>
            ))}
          </Select>
        </Field>

        <Field label="Observações (opcional)" htmlFor="maintenance-notes">
          <Input
            id="maintenance-notes"
            value={notes}
            onChange={(e) => setNotes(e.target.value)}
            placeholder="Ex.: Troca da lâmpada UV"
          />
        </Field>
      </form>
    </Modal>
  );
}
