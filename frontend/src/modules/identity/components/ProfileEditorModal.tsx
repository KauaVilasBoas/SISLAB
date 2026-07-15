import { useState, type FormEvent } from 'react';
import { Loader2 } from 'lucide-react';
import { Modal } from '@/shared/components/ui/modal';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import { Label } from '@/shared/components/ui/label';
import type { ApiError } from '@/shared/types/api';
import { useCreateProfile } from '@/modules/identity/api/identity.queries';
import { useToast } from '@/shared/components/ui/toast';

interface ProfileEditorModalProps {
  onClose: () => void;
}

/** Create a new authorization profile (name + description). Permissions are set on the edit page. */
export function ProfileEditorModal({ onClose }: ProfileEditorModalProps) {
  const createProfile = useCreateProfile();
  const toast = useToast();
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const saving = createProfile.isPending;

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    try {
      await createProfile.mutateAsync({ name: name.trim(), description: description.trim() });
      toast('success', 'Perfil criado com sucesso.');
      onClose();
    } catch (err) {
      toast('error', (err as ApiError)?.message ?? 'Não foi possível criar o perfil.');
    }
  }

  return (
    <Modal
      open
      onClose={onClose}
      title="Novo perfil"
      description="As permissões podem ser configuradas ao editar o perfil."
      footer={
        <>
          <Button variant="outline" onClick={onClose} disabled={saving}>
            Cancelar
          </Button>
          <Button type="submit" form="profile-create-form" disabled={saving}>
            {saving && <Loader2 className="size-4 animate-spin" />}
            Criar
          </Button>
        </>
      }
    >
      <form
        id="profile-create-form"
        className="flex flex-col gap-4"
        onSubmit={handleSubmit}
        noValidate
      >
        <div className="flex flex-col gap-2">
          <Label htmlFor="profile-name">Nome</Label>
          <Input
            id="profile-name"
            value={name}
            onChange={(e) => setName(e.target.value)}
            required
            autoFocus
          />
        </div>
        <div className="flex flex-col gap-2">
          <Label htmlFor="profile-description">Descrição</Label>
          <Input
            id="profile-description"
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            placeholder="Opcional"
          />
        </div>
      </form>
    </Modal>
  );
}
