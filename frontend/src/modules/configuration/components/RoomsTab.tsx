import { useState, type FormEvent } from 'react';
import { DoorOpen, Loader2, Plus } from 'lucide-react';
import { Button } from '@/shared/components/ui/button';
import { Card, CardContent } from '@/shared/components/ui/card';
import { Badge } from '@/shared/components/ui/badge';
import { Modal } from '@/shared/components/ui/modal';
import { Input } from '@/shared/components/ui/input';
import { Label } from '@/shared/components/ui/label';
import { useToast } from '@/shared/components/ui/toast';
import type { ApiError } from '@/shared/types/api';
import { useCreateRoom, useRooms } from '@/modules/configuration/api/configuration.queries';
import {
  CatalogueEmpty,
  CatalogueError,
  CatalogueLoading,
} from '@/modules/configuration/components/CatalogueState';

/** "Rooms" tab: lists rooms and creates new ones (name + requires-authorization flag). */
export function RoomsTab() {
  const rooms = useRooms();
  const [createOpen, setCreateOpen] = useState(false);

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-end">
        <Button onClick={() => setCreateOpen(true)}>
          <Plus className="size-4" />
          Nova sala
        </Button>
      </div>

      {rooms.isLoading ? (
        <CatalogueLoading label="Carregando salas…" />
      ) : rooms.isError ? (
        <CatalogueError label="Não foi possível carregar as salas." />
      ) : rooms.data && rooms.data.length > 0 ? (
        <Card>
          <CardContent className="p-0">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b text-left text-xs uppercase tracking-wide text-muted-foreground">
                  <th className="px-5 py-3 font-medium">Nome</th>
                  <th className="px-5 py-3 font-medium">Acesso</th>
                </tr>
              </thead>
              <tbody>
                {rooms.data.map((room) => (
                  <tr key={room.id} className="border-b last:border-0">
                    <td className="px-5 py-3 font-medium">{room.name}</td>
                    <td className="px-5 py-3">
                      {room.requiresAuthorization ? (
                        <Badge variant="secondary">Requer autorização</Badge>
                      ) : (
                        <span className="text-muted-foreground">Livre</span>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </CardContent>
        </Card>
      ) : (
        <CatalogueEmpty
          icon={<DoorOpen className="size-8" />}
          message="Nenhuma sala cadastrada. Crie a primeira sala do laboratório."
        />
      )}

      {createOpen ? <CreateRoomModal onClose={() => setCreateOpen(false)} /> : null}
    </div>
  );
}

function CreateRoomModal({ onClose }: { onClose: () => void }) {
  const create = useCreateRoom();
  const toast = useToast();
  const [name, setName] = useState('');
  const [requiresAuthorization, setRequiresAuthorization] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    try {
      await create.mutateAsync({ name: name.trim(), requiresAuthorization });
      toast('success', 'Sala criada com sucesso.');
      onClose();
    } catch (err) {
      toast('error', (err as ApiError)?.message ?? 'Não foi possível criar a sala.');
    }
  }

  return (
    <Modal
      open
      onClose={onClose}
      title="Nova sala"
      description="Cadastre um ambiente do laboratório para localizar itens e equipamentos."
      footer={
        <>
          <Button variant="outline" onClick={onClose} disabled={create.isPending}>
            Cancelar
          </Button>
          <Button type="submit" form="create-room-form" disabled={create.isPending}>
            {create.isPending && <Loader2 className="size-4 animate-spin" />}
            Criar sala
          </Button>
        </>
      }
    >
      <form id="create-room-form" className="flex flex-col gap-4" onSubmit={handleSubmit} noValidate>
        <div className="flex flex-col gap-2">
          <Label htmlFor="room-name">Nome</Label>
          <Input
            id="room-name"
            placeholder="Câmara fria"
            value={name}
            onChange={(e) => setName(e.target.value)}
            required
            autoFocus
          />
        </div>
        <label className="flex items-center gap-2 text-sm">
          <input
            type="checkbox"
            className="size-4 rounded border-input"
            checked={requiresAuthorization}
            onChange={(e) => setRequiresAuthorization(e.target.checked)}
          />
          Requer autorização para acesso
        </label>
      </form>
    </Modal>
  );
}
