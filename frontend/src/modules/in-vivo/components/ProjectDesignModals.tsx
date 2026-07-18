import { useState, type FormEvent } from 'react';
import { Loader2 } from 'lucide-react';
import { Modal } from '@/shared/components/ui/modal';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import { Label } from '@/shared/components/ui/label';
import { useToast } from '@/shared/components/ui/toast';
import type { ApiError } from '@/shared/types/api';
import type { AnimalSex } from '@/modules/in-vivo/types';
import { animalSexLabel } from '@/modules/in-vivo/presentation';
import {
  useAddAnimal,
  useAddBatch,
  useAddGroup,
} from '@/modules/in-vivo/api/projects.queries';

/** Add-a-batch (leva) form. */
export function AddBatchModal({
  projectId,
  onClose,
}: {
  projectId: string;
  onClose: () => void;
}) {
  const toast = useToast();
  const addBatch = useAddBatch(projectId);
  const [name, setName] = useState('');

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    try {
      await addBatch.mutateAsync(name.trim());
      toast('success', 'Leva adicionada.');
      onClose();
    } catch (err) {
      toast('error', (err as ApiError)?.message ?? 'Não foi possível adicionar a leva.');
    }
  }

  return (
    <Modal
      open
      onClose={onClose}
      title="Nova leva"
      description="Uma leva (batch) fixa a versão do desenho ao ser iniciada."
      footer={
        <>
          <Button variant="outline" onClick={onClose} disabled={addBatch.isPending}>
            Cancelar
          </Button>
          <Button type="submit" form="add-batch-form" disabled={addBatch.isPending}>
            {addBatch.isPending && <Loader2 className="size-4 animate-spin" />}
            Adicionar leva
          </Button>
        </>
      }
    >
      <form id="add-batch-form" className="space-y-4" onSubmit={handleSubmit} noValidate>
        <div className="space-y-1.5">
          <Label htmlFor="batch-name">Nome</Label>
          <Input
            id="batch-name"
            value={name}
            onChange={(e) => setName(e.target.value)}
            placeholder="Ex.: Leva 1 — junho/2026"
            maxLength={120}
            required
          />
        </div>
      </form>
    </Modal>
  );
}

/** Add-a-dose-group form. */
export function AddGroupModal({
  projectId,
  batchId,
  onClose,
}: {
  projectId: string;
  batchId: string;
  onClose: () => void;
}) {
  const toast = useToast();
  const addGroup = useAddGroup(projectId, batchId);
  const [name, setName] = useState('');
  const [doseAmount, setDoseAmount] = useState('0');
  const [doseUnit, setDoseUnit] = useState('mg/kg');

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    try {
      await addGroup.mutateAsync({
        name: name.trim(),
        doseAmount: Number(doseAmount),
        doseUnit: doseUnit.trim(),
      });
      toast('success', 'Grupo adicionado.');
      onClose();
    } catch (err) {
      toast('error', (err as ApiError)?.message ?? 'Não foi possível adicionar o grupo.');
    }
  }

  return (
    <Modal
      open
      onClose={onClose}
      title="Novo grupo (dose)"
      description="Braço de tratamento — dose 0 modela o veículo/controle."
      footer={
        <>
          <Button variant="outline" onClick={onClose} disabled={addGroup.isPending}>
            Cancelar
          </Button>
          <Button type="submit" form="add-group-form" disabled={addGroup.isPending}>
            {addGroup.isPending && <Loader2 className="size-4 animate-spin" />}
            Adicionar grupo
          </Button>
        </>
      }
    >
      <form id="add-group-form" className="space-y-4" onSubmit={handleSubmit} noValidate>
        <div className="space-y-1.5">
          <Label htmlFor="group-name">Nome</Label>
          <Input
            id="group-name"
            value={name}
            onChange={(e) => setName(e.target.value)}
            placeholder="Ex.: Controle (veículo)"
            maxLength={120}
            required
          />
        </div>
        <div className="grid grid-cols-2 gap-3">
          <div className="space-y-1.5">
            <Label htmlFor="group-dose">Dose</Label>
            <Input
              id="group-dose"
              type="number"
              min={0}
              step="any"
              value={doseAmount}
              onChange={(e) => setDoseAmount(e.target.value)}
              required
            />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="group-unit">Unidade</Label>
            <Input
              id="group-unit"
              value={doseUnit}
              onChange={(e) => setDoseUnit(e.target.value)}
              placeholder="mg/kg"
              maxLength={30}
              required
            />
          </div>
        </div>
      </form>
    </Modal>
  );
}

const SEXES: AnimalSex[] = ['Male', 'Female'];

/** Enrol-an-animal form. */
export function AddAnimalModal({
  projectId,
  batchId,
  groupId,
  onClose,
}: {
  projectId: string;
  batchId: string;
  groupId: string;
  onClose: () => void;
}) {
  const toast = useToast();
  const addAnimal = useAddAnimal(projectId, batchId, groupId);
  const [identifier, setIdentifier] = useState('');
  const [sex, setSex] = useState<AnimalSex>('Male');
  const [weight, setWeight] = useState('');

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    try {
      await addAnimal.mutateAsync({
        identifier: identifier.trim(),
        sex,
        weightGrams: weight.trim() === '' ? null : Number(weight),
      });
      toast('success', 'Animal cadastrado.');
      onClose();
    } catch (err) {
      toast(
        'error',
        (err as ApiError)?.message ?? 'Não foi possível cadastrar o animal.',
      );
    }
  }

  return (
    <Modal
      open
      onClose={onClose}
      title="Cadastrar animal"
      description="Identificador único no projeto (brinco / código de gaiola)."
      footer={
        <>
          <Button variant="outline" onClick={onClose} disabled={addAnimal.isPending}>
            Cancelar
          </Button>
          <Button type="submit" form="add-animal-form" disabled={addAnimal.isPending}>
            {addAnimal.isPending && <Loader2 className="size-4 animate-spin" />}
            Cadastrar animal
          </Button>
        </>
      }
    >
      <form id="add-animal-form" className="space-y-4" onSubmit={handleSubmit} noValidate>
        <div className="space-y-1.5">
          <Label htmlFor="animal-id">Identificador</Label>
          <Input
            id="animal-id"
            value={identifier}
            onChange={(e) => setIdentifier(e.target.value)}
            placeholder="Ex.: M1-07"
            maxLength={60}
            required
          />
        </div>
        <div className="space-y-1.5">
          <Label>Sexo</Label>
          <div className="grid grid-cols-2 gap-2">
            {SEXES.map((option) => {
              const selected = option === sex;
              return (
                <button
                  key={option}
                  type="button"
                  onClick={() => setSex(option)}
                  aria-pressed={selected}
                  className={
                    selected
                      ? 'rounded-md border border-primary bg-primary/5 px-3 py-2 text-sm font-medium text-primary'
                      : 'rounded-md border px-3 py-2 text-sm text-muted-foreground hover:border-primary/50'
                  }
                >
                  {animalSexLabel[option]}
                </button>
              );
            })}
          </div>
        </div>
        <div className="space-y-1.5">
          <Label htmlFor="animal-weight">Peso (g) — opcional</Label>
          <Input
            id="animal-weight"
            type="number"
            min={0}
            step="any"
            value={weight}
            onChange={(e) => setWeight(e.target.value)}
            placeholder="Ex.: 250"
          />
        </div>
      </form>
    </Modal>
  );
}
