import { useState, type FormEvent } from 'react';
import { Loader2 } from 'lucide-react';
import { Modal } from '@/shared/components/ui/modal';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import { Label } from '@/shared/components/ui/label';
import { SingleSelect } from '@/shared/components/ui/single-select';
import { useToast } from '@/shared/components/ui/toast';
import type { ApiError } from '@/shared/types/api';
import type { AnimalSex, GroupDetail } from '@/modules/in-vivo/types';
import { animalSexLabel, formatAmount } from '@/modules/in-vivo/presentation';
import {
  useAddAnimal,
  useAddBatch,
  useAddCage,
  useAddGroup,
  useAssignAnimalToGroup,
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

/** Add-a-cage (caixa) form (SISLAB-03) — identifier + optional capacity (e.g. 4). */
export function AddCageModal({
  projectId,
  batchId,
  onClose,
}: {
  projectId: string;
  batchId: string;
  onClose: () => void;
}) {
  const toast = useToast();
  const addCage = useAddCage(projectId, batchId);
  const [name, setName] = useState('');
  const [capacity, setCapacity] = useState('4');

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    try {
      await addCage.mutateAsync({
        name: name.trim(),
        capacity: capacity.trim() === '' ? null : Number(capacity),
      });
      toast('success', 'Caixa adicionada.');
      onClose();
    } catch (err) {
      toast('error', (err as ApiError)?.message ?? 'Não foi possível adicionar a caixa.');
    }
  }

  return (
    <Modal
      open
      onClose={onClose}
      title="Nova caixa"
      description="Unidade de alojamento pré-randomização. A capacidade (ex.: 4) é opcional."
      footer={
        <>
          <Button variant="outline" onClick={onClose} disabled={addCage.isPending}>
            Cancelar
          </Button>
          <Button type="submit" form="add-cage-form" disabled={addCage.isPending}>
            {addCage.isPending && <Loader2 className="size-4 animate-spin" />}
            Adicionar caixa
          </Button>
        </>
      }
    >
      <form id="add-cage-form" className="space-y-4" onSubmit={handleSubmit} noValidate>
        <div className="space-y-1.5">
          <Label htmlFor="cage-name">Identificador</Label>
          <Input
            id="cage-name"
            value={name}
            onChange={(e) => setName(e.target.value)}
            placeholder="Ex.: CX1"
            maxLength={60}
            required
          />
        </div>
        <div className="space-y-1.5">
          <Label htmlFor="cage-capacity">Capacidade — opcional</Label>
          <Input
            id="cage-capacity"
            type="number"
            min={1}
            step={1}
            value={capacity}
            onChange={(e) => setCapacity(e.target.value)}
            placeholder="Ex.: 4"
          />
          <p className="text-xs text-muted-foreground">
            Nº máximo de animais na caixa. Deixe em branco para não limitar.
          </p>
        </div>
      </form>
    </Modal>
  );
}

const SEXES: AnimalSex[] = ['Male', 'Female'];

/**
 * House-an-animal form (SISLAB-03) — the new flow: an animal enters a CAGE, its treatment group is an
 * optional assignment made later (after basal). The cage identifier is shown for context.
 */
export function AddAnimalModal({
  projectId,
  batchId,
  cageId,
  cageName,
  onClose,
}: {
  projectId: string;
  batchId: string;
  cageId: string;
  cageName: string;
  onClose: () => void;
}) {
  const toast = useToast();
  const addAnimal = useAddAnimal(projectId, batchId, cageId);
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
      toast('success', 'Animal cadastrado na caixa.');
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
      title={`Cadastrar animal — ${cageName}`}
      description="O animal entra na caixa sem grupo. A dose é atribuída depois do basal."
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
            placeholder="Ex.: A1"
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

/**
 * Assign-or-move-to-a-group form (SISLAB-03) — the randomization step run after basal/induction, and the
 * way a discrepant cage is redistributed. Picks among the batch's dose groups; the currently assigned group
 * (if any) is pre-selected so a move is one click. Locked once the leva starts (the caller hides it then).
 */
export function AssignAnimalToGroupModal({
  projectId,
  batchId,
  animalId,
  animalIdentifier,
  currentGroupId,
  groups,
  onClose,
}: {
  projectId: string;
  batchId: string;
  animalId: string;
  animalIdentifier: string;
  currentGroupId: string | null;
  groups: GroupDetail[];
  onClose: () => void;
}) {
  const toast = useToast();
  const assign = useAssignAnimalToGroup(projectId, batchId);
  const [groupId, setGroupId] = useState<string | null>(currentGroupId);

  const options = groups.map((group) => ({
    value: group.id,
    label: group.name,
    hint: formatAmount(group.doseAmount, group.doseUnit),
  }));

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!groupId) {
      toast('error', 'Escolha um grupo.');
      return;
    }
    try {
      await assign.mutateAsync({ animalId, body: { groupId } });
      toast('success', 'Animal atribuído ao grupo.');
      onClose();
    } catch (err) {
      toast('error', (err as ApiError)?.message ?? 'Não foi possível atribuir o animal.');
    }
  }

  return (
    <Modal
      open
      onClose={onClose}
      title={`Atribuir a grupo — ${animalIdentifier}`}
      description="Randomização pós-basal: mova o animal para o braço de dose. Redistribui caixas discrepantes."
      footer={
        <>
          <Button variant="outline" onClick={onClose} disabled={assign.isPending}>
            Cancelar
          </Button>
          <Button
            type="submit"
            form="assign-group-form"
            disabled={assign.isPending || !groupId}
          >
            {assign.isPending && <Loader2 className="size-4 animate-spin" />}
            {currentGroupId ? 'Mover animal' : 'Atribuir animal'}
          </Button>
        </>
      }
    >
      <form id="assign-group-form" className="space-y-4" onSubmit={handleSubmit} noValidate>
        {groups.length === 0 ? (
          <p className="rounded-md border bg-muted/30 p-3 text-sm text-muted-foreground">
            Nenhum grupo cadastrado nesta leva. Adicione braços de dose antes de atribuir.
          </p>
        ) : (
          <div className="space-y-1.5">
            <Label>Grupo (dose)</Label>
            <SingleSelect
              label="Grupo de dose"
              options={options}
              value={groupId}
              onChange={setGroupId}
              placeholder="Escolher grupo…"
              className="w-full"
            />
          </div>
        )}
      </form>
    </Modal>
  );
}
