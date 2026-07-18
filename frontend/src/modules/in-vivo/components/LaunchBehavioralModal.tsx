import { useState, type FormEvent } from 'react';
import { Loader2 } from 'lucide-react';
import { Modal } from '@/shared/components/ui/modal';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import { Label } from '@/shared/components/ui/label';
import { useToast } from '@/shared/components/ui/toast';
import type { ApiError } from '@/shared/types/api';
import type { BehavioralType } from '@/modules/in-vivo/types';
import { behavioralTypePresentation } from '@/modules/in-vivo/presentation';
import { useCreateBehavioralExperiment } from '@/modules/in-vivo/api/behavioral.queries';

const TYPES: BehavioralType[] = ['VonFrei', 'TailFlick', 'RotaRod', 'Hemograma'];

interface LaunchBehavioralModalProps {
  projectId: string;
  batchId: string;
  onClose: () => void;
  onLaunched: (experimentId: string) => void;
}

/**
 * Launch-a-behavioural-experiment form (card [E11] #88): pick the assay type, title it and declare the timepoint
 * labels (comma-separated) the experiment will record. Bound to a project + batch by value. On success it hands
 * the new experiment id back so the caller can open its detail (where timepoints are recorded).
 */
export function LaunchBehavioralModal({
  projectId,
  batchId,
  onClose,
  onLaunched,
}: LaunchBehavioralModalProps) {
  const toast = useToast();
  const create = useCreateBehavioralExperiment();

  const [type, setType] = useState<BehavioralType>('VonFrei');
  const [title, setTitle] = useState('');
  const [timepoints, setTimepoints] = useState('Baseline, T30, T60');

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const labels = timepoints
      .split(',')
      .map((label) => label.trim())
      .filter((label) => label.length > 0);

    if (labels.length === 0) {
      toast('error', 'Informe ao menos um timepoint.');
      return;
    }

    try {
      const id = await create.mutateAsync({
        type,
        title: title.trim(),
        description: null,
        projectId,
        batchId,
        timepointLabels: labels,
      });
      toast('success', 'Teste comportamental criado.');
      onLaunched(id);
    } catch (err) {
      toast('error', (err as ApiError)?.message ?? 'Não foi possível criar o teste.');
    }
  }

  return (
    <Modal
      open
      onClose={onClose}
      title="Lançar teste comportamental"
      description={behavioralTypePresentation[type].description}
      footer={
        <>
          <Button variant="outline" onClick={onClose} disabled={create.isPending}>
            Cancelar
          </Button>
          <Button type="submit" form="launch-behavioral-form" disabled={create.isPending}>
            {create.isPending && <Loader2 className="size-4 animate-spin" />}
            Criar teste
          </Button>
        </>
      }
    >
      <form
        id="launch-behavioral-form"
        className="space-y-4"
        onSubmit={handleSubmit}
        noValidate
      >
        <div className="space-y-1.5">
          <Label>Tipo de teste</Label>
          <div className="grid grid-cols-2 gap-2">
            {TYPES.map((option) => {
              const selected = option === type;
              return (
                <button
                  key={option}
                  type="button"
                  onClick={() => setType(option)}
                  aria-pressed={selected}
                  className={
                    selected
                      ? 'rounded-md border border-primary bg-primary/5 px-3 py-2 text-left text-sm font-medium text-primary'
                      : 'rounded-md border px-3 py-2 text-left text-sm text-muted-foreground hover:border-primary/50'
                  }
                >
                  {behavioralTypePresentation[option].label}
                </button>
              );
            })}
          </div>
        </div>

        <div className="space-y-1.5">
          <Label htmlFor="behavioral-title">Título</Label>
          <Input
            id="behavioral-title"
            value={title}
            onChange={(e) => setTitle(e.target.value)}
            placeholder="Ex.: Von Frey — leva 1"
            maxLength={200}
            required
          />
        </div>

        <div className="space-y-1.5">
          <Label htmlFor="behavioral-timepoints">
            Timepoints (separados por vírgula)
          </Label>
          <Input
            id="behavioral-timepoints"
            value={timepoints}
            onChange={(e) => setTimepoints(e.target.value)}
            placeholder="Baseline, T30, T60"
            required
          />
          <p className="text-xs text-muted-foreground">
            Cada timepoint vira uma etapa de lançamento no fluxo do experimento.
          </p>
        </div>
      </form>
    </Modal>
  );
}
