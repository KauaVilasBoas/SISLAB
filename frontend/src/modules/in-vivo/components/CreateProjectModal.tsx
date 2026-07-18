import { useState, type FormEvent } from 'react';
import { Loader2 } from 'lucide-react';
import { Modal } from '@/shared/components/ui/modal';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import { Label } from '@/shared/components/ui/label';
import { useToast } from '@/shared/components/ui/toast';
import type { ApiError } from '@/shared/types/api';
import { useCreateProject } from '@/modules/in-vivo/api/projects.queries';

interface CreateProjectModalProps {
  onClose: () => void;
  onCreated: (id: string) => void;
}

/**
 * Create-a-project form (card [E11] #73): name (required), the animal species under study and an optional
 * description. The project starts in Draft; batches/groups/animals are added from the detail. On success it
 * invalidates the list and hands the new id back so the caller can open its detail.
 */
export function CreateProjectModal({ onClose, onCreated }: CreateProjectModalProps) {
  const toast = useToast();
  const create = useCreateProject();

  const [name, setName] = useState('');
  const [species, setSpecies] = useState('');
  const [description, setDescription] = useState('');

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    try {
      const id = await create.mutateAsync({
        name: name.trim(),
        species: species.trim(),
        description: description.trim() === '' ? null : description.trim(),
      });
      toast('success', 'Projeto criado.');
      onCreated(id);
    } catch (err) {
      toast('error', (err as ApiError)?.message ?? 'Não foi possível criar o projeto.');
    }
  }

  return (
    <Modal
      open
      onClose={onClose}
      title="Novo projeto in vivo"
      description="Delineamento experimental — Projeto → Leva → Grupo → Animal."
      footer={
        <>
          <Button variant="outline" onClick={onClose} disabled={create.isPending}>
            Cancelar
          </Button>
          <Button type="submit" form="create-project-form" disabled={create.isPending}>
            {create.isPending && <Loader2 className="size-4 animate-spin" />}
            Criar projeto
          </Button>
        </>
      }
    >
      <form
        id="create-project-form"
        className="space-y-4"
        onSubmit={handleSubmit}
        noValidate
      >
        <div className="space-y-1.5">
          <Label htmlFor="project-name">Nome</Label>
          <Input
            id="project-name"
            value={name}
            onChange={(e) => setName(e.target.value)}
            placeholder="Ex.: Neuropatia — composto GDA-12"
            maxLength={200}
            required
          />
        </div>

        <div className="space-y-1.5">
          <Label htmlFor="project-species">Espécie</Label>
          <Input
            id="project-species"
            value={species}
            onChange={(e) => setSpecies(e.target.value)}
            placeholder="Ex.: Rattus norvegicus (Wistar)"
            maxLength={120}
            required
          />
        </div>

        <div className="space-y-1.5">
          <Label htmlFor="project-description">Descrição (opcional)</Label>
          <textarea
            id="project-description"
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            placeholder="Objetivo do estudo, protocolo, observações."
            maxLength={2000}
            rows={3}
            className="flex w-full rounded-md border border-input bg-transparent px-3 py-2 text-sm shadow-sm placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
          />
        </div>
      </form>
    </Modal>
  );
}
