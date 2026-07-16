import { useState, type FormEvent } from 'react';
import { Loader2 } from 'lucide-react';
import { Modal } from '@/shared/components/ui/modal';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import type { ApiError } from '@/shared/types/api';
import { useToast } from '@/shared/components/ui/toast';
import { Field } from '@/modules/inventory/components/form-controls';
import { useDefineEquipmentCalibration } from '@/modules/inventory/api/equipment.queries';
import type {
  DefineEquipmentCalibrationRequest,
  EquipmentDetail,
} from '@/modules/inventory/equipment.types';

interface EquipmentCalibrationModalProps {
  equipment: EquipmentDetail;
  onClose: () => void;
}

/** Turns a blank string into null; a cleared date means "no scheduled calibration". */
function orNull(value: string): string | null {
  return value.trim() === '' ? null : value.trim();
}

/**
 * Defines or clears an equipment's calibration schedule (card [E7] #48). Pre-filled with the current
 * dates; clearing "próxima calibração" removes the schedule (the derived status then reads N/A). On
 * success it invalidates the equipment namespace so the table's "Últ. calibração" column and its
 * overdue highlight refresh.
 */
export function EquipmentCalibrationModal({
  equipment,
  onClose,
}: EquipmentCalibrationModalProps) {
  const toast = useToast();
  const define = useDefineEquipmentCalibration(equipment.id);

  const [lastCalibration, setLastCalibration] = useState(
    equipment.lastCalibrationDate ?? '',
  );
  const [nextCalibration, setNextCalibration] = useState(
    equipment.nextCalibrationDate ?? '',
  );

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const body: DefineEquipmentCalibrationRequest = {
      lastCalibration: orNull(lastCalibration),
      nextCalibration: orNull(nextCalibration),
    };
    try {
      await define.mutateAsync(body);
      toast('success', 'Calibração atualizada.');
      onClose();
    } catch (err) {
      toast(
        'error',
        (err as ApiError)?.message ?? 'Não foi possível atualizar a calibração.',
      );
    }
  }

  return (
    <Modal
      open
      onClose={onClose}
      title="Calibração"
      description="Defina as datas de calibração. Deixe a próxima em branco para remover o agendamento."
      footer={
        <>
          <Button variant="outline" onClick={onClose} disabled={define.isPending}>
            Cancelar
          </Button>
          <Button
            type="submit"
            form="equipment-calibration-form"
            disabled={define.isPending}
          >
            {define.isPending && <Loader2 className="size-4 animate-spin" />}
            Salvar
          </Button>
        </>
      }
    >
      <form
        id="equipment-calibration-form"
        className="grid grid-cols-1 gap-4"
        onSubmit={handleSubmit}
        noValidate
      >
        <Field label="Última calibração" htmlFor="calibration-last">
          <Input
            id="calibration-last"
            type="date"
            value={lastCalibration}
            onChange={(e) => setLastCalibration(e.target.value)}
          />
        </Field>

        <Field label="Próxima calibração" htmlFor="calibration-next">
          <Input
            id="calibration-next"
            type="date"
            value={nextCalibration}
            onChange={(e) => setNextCalibration(e.target.value)}
          />
        </Field>
      </form>
    </Modal>
  );
}
