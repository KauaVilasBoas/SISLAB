import { useState, type FormEvent } from 'react';
import { Loader2 } from 'lucide-react';
import { Modal } from '@/shared/components/ui/modal';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import { Label } from '@/shared/components/ui/label';
import { useToast } from '@/shared/components/ui/toast';
import type { ApiError } from '@/shared/types/api';
import type { ExperimentType } from '@/modules/experiments/types';
import { experimentTypePresentation } from '@/modules/experiments/components/experiment-presentation';
import { useCreateExperiment } from '@/modules/experiments/api/experiments.queries';

const EXPERIMENT_TYPES: ExperimentType[] = ['ViabilidadeCelular', 'NitricOxide'];

interface CreateExperimentModalProps {
  onClose: () => void;
  /** Called with the new experiment id after a successful creation (e.g. to navigate to its detail). */
  onCreated: (id: string) => void;
}

/**
 * Create-an-experiment form (cards [E11] #68 / #72). Title (required), an optional description and the assay
 * type (cell viability or nitric oxide) — both plate assays share the same create flow. On success it
 * invalidates the list and hands the new id back so the caller can open its detail.
 */
export function CreateExperimentModal({ onClose, onCreated }: CreateExperimentModalProps) {
  const toast = useToast();
  const create = useCreateExperiment();

  const [type, setType] = useState<ExperimentType>('ViabilidadeCelular');
  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    try {
      const id = await create.mutateAsync({
        type,
        title: title.trim(),
        description: description.trim() === '' ? null : description.trim(),
        compoundPartnerId: null,
      });
      toast('success', 'Experimento criado.');
      onCreated(id);
    } catch (err) {
      toast('error', (err as ApiError)?.message ?? 'Não foi possível criar o experimento.');
    }
  }

  return (
    <Modal
      open
      onClose={onClose}
      title="Novo experimento"
      description={experimentTypePresentation[type].description}
      footer={
        <>
          <Button variant="outline" onClick={onClose} disabled={create.isPending}>
            Cancelar
          </Button>
          <Button type="submit" form="create-experiment-form" disabled={create.isPending}>
            {create.isPending && <Loader2 className="size-4 animate-spin" />}
            Criar experimento
          </Button>
        </>
      }
    >
      <form id="create-experiment-form" className="space-y-4" onSubmit={handleSubmit} noValidate>
        <div className="space-y-1.5">
          <Label>Tipo de ensaio</Label>
          <div className="grid grid-cols-2 gap-2">
            {EXPERIMENT_TYPES.map((option) => {
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
                  {experimentTypePresentation[option].label}
                </button>
              );
            })}
          </div>
        </div>

        <div className="space-y-1.5">
          <Label htmlFor="exp-title">Título</Label>
          <Input
            id="exp-title"
            value={title}
            onChange={(e) => setTitle(e.target.value)}
            placeholder="Ex.: MTT — composto GDA-12"
            maxLength={200}
            required
          />
        </div>

        <div className="space-y-1.5">
          <Label htmlFor="exp-description">Descrição (opcional)</Label>
          <textarea
            id="exp-description"
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            placeholder="Contexto do ensaio, protocolo, observações."
            maxLength={2000}
            rows={3}
            className="flex w-full rounded-md border border-input bg-transparent px-3 py-2 text-sm shadow-sm placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
          />
        </div>
      </form>
    </Modal>
  );
}
