import { useState, type FormEvent } from 'react';
import { Loader2, Plus, Users } from 'lucide-react';
import { Button } from '@/shared/components/ui/button';
import { Card, CardContent } from '@/shared/components/ui/card';
import { Modal } from '@/shared/components/ui/modal';
import { Input } from '@/shared/components/ui/input';
import { Label } from '@/shared/components/ui/label';
import { useToast } from '@/shared/components/ui/toast';
import type { ApiError } from '@/shared/types/api';
import { RequirePermission } from '@/modules/auth/PermissionsProvider';
import { Permissions } from '@/modules/auth/permissions';
import {
  useCollectionRoles,
  useCreateCollectionRole,
} from '@/modules/configuration/api/configuration.queries';
import {
  CatalogueEmpty,
  CatalogueError,
  CatalogueLoading,
} from '@/modules/configuration/components/CatalogueState';

/**
 * "Funções de coleta" tab (SISLAB-08): lists the active company's configurable collection jobs — e.g. Volante,
 * Anestesia, Decapitação, Sangue, Medula, Gânglio, Nervo — and cadasters new ones. These roles are assigned to
 * members on a batch's collection plan (in vivo screen); the list is never hardcoded.
 */
export function CollectionRolesTab() {
  const roles = useCollectionRoles();
  const [createOpen, setCreateOpen] = useState(false);

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-end">
        <RequirePermission code={Permissions.configuration.createCollectionRole}>
          <Button onClick={() => setCreateOpen(true)}>
            <Plus className="size-4" />
            Nova função
          </Button>
        </RequirePermission>
      </div>

      {roles.isLoading ? (
        <CatalogueLoading label="Carregando funções de coleta…" />
      ) : roles.isError ? (
        <CatalogueError label="Não foi possível carregar as funções de coleta." />
      ) : roles.data && roles.data.length > 0 ? (
        <Card>
          <CardContent className="p-0">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b text-left text-xs uppercase tracking-wide text-muted-foreground">
                  <th className="px-5 py-3 font-medium">Função</th>
                  <th className="px-5 py-3 font-medium">Descrição</th>
                </tr>
              </thead>
              <tbody>
                {roles.data.map((role) => (
                  <tr key={role.id} className="border-b last:border-0">
                    <td className="px-5 py-3 font-medium">{role.name}</td>
                    <td className="px-5 py-3 text-muted-foreground">
                      {role.description ?? '—'}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </CardContent>
        </Card>
      ) : (
        <CatalogueEmpty
          icon={<Users className="size-8" />}
          message="Nenhuma função cadastrada. Crie a primeira (ex.: Volante, Sangue, Medula)."
        />
      )}

      {createOpen ? (
        <CreateCollectionRoleModal onClose={() => setCreateOpen(false)} />
      ) : null}
    </div>
  );
}

function CreateCollectionRoleModal({ onClose }: { onClose: () => void }) {
  const create = useCreateCollectionRole();
  const toast = useToast();
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    const trimmedName = name.trim();
    if (!trimmedName) {
      toast('error', 'Informe o nome da função.');
      return;
    }

    try {
      await create.mutateAsync({
        name: trimmedName,
        description: description.trim() || null,
      });
      toast('success', 'Função de coleta criada com sucesso.');
      onClose();
    } catch (err) {
      toast('error', (err as ApiError)?.message ?? 'Não foi possível criar a função.');
    }
  }

  return (
    <Modal
      open
      onClose={onClose}
      title="Nova função de coleta"
      description="Tarefa da coleta atribuível a um membro (ex.: Volante, Anestesia, Sangue)."
      footer={
        <>
          <Button variant="outline" onClick={onClose} disabled={create.isPending}>
            Cancelar
          </Button>
          <Button
            type="submit"
            form="create-collection-role-form"
            disabled={create.isPending}
          >
            {create.isPending && <Loader2 className="size-4 animate-spin" />}
            Criar função
          </Button>
        </>
      }
    >
      <form
        id="create-collection-role-form"
        className="flex flex-col gap-4"
        onSubmit={handleSubmit}
        noValidate
      >
        <div className="flex flex-col gap-2">
          <Label htmlFor="collection-role-name">Nome</Label>
          <Input
            id="collection-role-name"
            placeholder="Sangue"
            value={name}
            onChange={(e) => setName(e.target.value)}
            required
            autoFocus
          />
        </div>
        <div className="flex flex-col gap-2">
          <Label htmlFor="collection-role-description">Descrição (opcional)</Label>
          <Input
            id="collection-role-description"
            placeholder="Coleta de sangue por punção cardíaca"
            value={description}
            onChange={(e) => setDescription(e.target.value)}
          />
        </div>
      </form>
    </Modal>
  );
}
