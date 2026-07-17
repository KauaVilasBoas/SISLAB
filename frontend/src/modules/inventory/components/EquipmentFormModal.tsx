import { useState, type FormEvent } from 'react';
import { Loader2 } from 'lucide-react';
import { Modal } from '@/shared/components/ui/modal';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import type { ApiError } from '@/shared/types/api';
import { useToast } from '@/shared/components/ui/toast';
import { Field, Select } from '@/modules/inventory/components/form-controls';
import { useStorageLocations } from '@/modules/inventory/api/inventory.queries';
import {
  useRegisterEquipment,
  useUpdateEquipment,
} from '@/modules/inventory/api/equipment.queries';
import {
  EQUIPMENT_STATUSES,
  equipmentStatusPresentation,
} from '@/modules/inventory/components/equipment-presentation';
import type {
  EquipmentDetail,
  EquipmentStatus,
  RegisterEquipmentRequest,
  UpdateEquipmentRequest,
} from '@/modules/inventory/equipment.types';

interface EquipmentFormModalProps {
  /** When provided, the modal edits this equipment; otherwise it registers a new one. */
  equipment?: EquipmentDetail;
  onClose: () => void;
}

interface FormState {
  name: string;
  assetTag: string;
  brand: string;
  model: string;
  storageLocationId: string;
  status: EquipmentStatus;
  lastCalibration: string;
  nextCalibration: string;
}

function initialState(equipment?: EquipmentDetail): FormState {
  return {
    name: equipment?.name ?? '',
    assetTag: equipment?.assetTag ?? '',
    brand: equipment?.manufacturer ?? '',
    model: equipment?.model ?? '',
    storageLocationId: equipment?.storageLocationId ?? '',
    status: equipment?.status ?? 'Available',
    lastCalibration: equipment?.lastCalibrationDate ?? '',
    nextCalibration: equipment?.nextCalibrationDate ?? '',
  };
}

/** Turns a blank string into null; used for the optional text/date fields the backend accepts. */
function orNull(value: string): string | null {
  const trimmed = value.trim();
  return trimmed === '' ? null : trimmed;
}

/**
 * Create/edit-an-equipment form (card [E7] #48). On create it posts the full RegisterEquipmentRequest
 * (identity + initial status + optional calibration dates); on edit it puts only the identification
 * data the update endpoint accepts (status and calibration are changed from the detail sheet, each via
 * its own aggregate command). Both variants drive the location dropdown from the storage-location
 * catalogue and, on success, invalidate the equipment namespace and close the modal.
 */
export function EquipmentFormModal({ equipment, onClose }: EquipmentFormModalProps) {
  const isEdit = Boolean(equipment);
  const toast = useToast();
  const register = useRegisterEquipment();
  const update = useUpdateEquipment(equipment?.id ?? '');
  const locations = useStorageLocations();

  const [form, setForm] = useState<FormState>(() => initialState(equipment));

  const pending = register.isPending || update.isPending;

  function patch<K extends keyof FormState>(key: K, value: FormState[K]) {
    setForm((prev) => ({ ...prev, [key]: value }));
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    try {
      if (isEdit) {
        const body: UpdateEquipmentRequest = {
          name: form.name.trim(),
          assetTag: form.assetTag.trim(),
          brand: orNull(form.brand),
          model: orNull(form.model),
          storageLocationId: orNull(form.storageLocationId),
        };
        await update.mutateAsync(body);
        toast('success', 'Equipamento atualizado.');
      } else {
        const body: RegisterEquipmentRequest = {
          name: form.name.trim(),
          assetTag: form.assetTag.trim(),
          brand: orNull(form.brand),
          model: orNull(form.model),
          storageLocationId: orNull(form.storageLocationId),
          status: form.status,
          lastCalibration: orNull(form.lastCalibration),
          nextCalibration: orNull(form.nextCalibration),
        };
        await register.mutateAsync(body);
        toast('success', 'Equipamento cadastrado.');
      }
      onClose();
    } catch (err) {
      toast(
        'error',
        (err as ApiError)?.message ?? 'Não foi possível salvar o equipamento.',
      );
    }
  }

  return (
    <Modal
      open
      onClose={onClose}
      size="lg"
      title={isEdit ? 'Editar equipamento' : 'Novo equipamento'}
      description={
        isEdit
          ? 'Atualize a identificação e a localização do equipamento.'
          : 'Cadastre um equipamento com seu tombamento, status e calibração.'
      }
      footer={
        <>
          <Button variant="outline" onClick={onClose} disabled={pending}>
            Cancelar
          </Button>
          <Button type="submit" form="equipment-form" disabled={pending}>
            {pending && <Loader2 className="size-4 animate-spin" />}
            {isEdit ? 'Salvar alterações' : 'Cadastrar equipamento'}
          </Button>
        </>
      }
    >
      <form
        id="equipment-form"
        className="grid grid-cols-1 gap-4 sm:grid-cols-2"
        onSubmit={handleSubmit}
        noValidate
      >
        <Field label="Nome" htmlFor="eq-name" className="sm:col-span-2">
          <Input
            id="eq-name"
            value={form.name}
            onChange={(e) => patch('name', e.target.value)}
            placeholder="Ex.: Espectrofotômetro UV-Vis"
            required
            autoFocus
          />
        </Field>

        <Field label="Tombamento (patrimônio)" htmlFor="eq-asset-tag">
          <Input
            id="eq-asset-tag"
            value={form.assetTag}
            onChange={(e) => patch('assetTag', e.target.value)}
            placeholder="Ex.: PAT-0041"
            required
          />
        </Field>

        <Field label="Local de armazenamento" htmlFor="eq-location">
          <Select
            id="eq-location"
            value={form.storageLocationId}
            onChange={(e) => patch('storageLocationId', e.target.value)}
          >
            <option value="">Sem local definido</option>
            {(locations.data ?? []).map((l) => (
              <option key={l.id} value={l.id}>
                {l.name}
              </option>
            ))}
          </Select>
        </Field>

        <Field label="Marca (opcional)" htmlFor="eq-brand">
          <Input
            id="eq-brand"
            value={form.brand}
            onChange={(e) => patch('brand', e.target.value)}
          />
        </Field>

        <Field label="Modelo (opcional)" htmlFor="eq-model">
          <Input
            id="eq-model"
            value={form.model}
            onChange={(e) => patch('model', e.target.value)}
          />
        </Field>

        {isEdit ? null : (
          <>
            <Field label="Status" htmlFor="eq-status">
              <Select
                id="eq-status"
                value={form.status}
                onChange={(e) => patch('status', e.target.value as EquipmentStatus)}
              >
                {EQUIPMENT_STATUSES.map((status) => (
                  <option key={status} value={status}>
                    {equipmentStatusPresentation(status).label}
                  </option>
                ))}
              </Select>
            </Field>

            <div className="hidden sm:block" aria-hidden />

            <Field label="Última calibração (opcional)" htmlFor="eq-last-calibration">
              <Input
                id="eq-last-calibration"
                type="date"
                value={form.lastCalibration}
                onChange={(e) => patch('lastCalibration', e.target.value)}
              />
            </Field>

            <Field label="Próxima calibração (opcional)" htmlFor="eq-next-calibration">
              <Input
                id="eq-next-calibration"
                type="date"
                value={form.nextCalibration}
                onChange={(e) => patch('nextCalibration', e.target.value)}
              />
            </Field>
          </>
        )}
      </form>
    </Modal>
  );
}
