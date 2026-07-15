import { useState, type FormEvent } from 'react';
import { Loader2 } from 'lucide-react';
import { Modal } from '@/shared/components/ui/modal';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import { Label } from '@/shared/components/ui/label';
import type { ApiError } from '@/shared/types/api';
import type { ProfileDto } from '@/modules/identity/types';
import { useInviteMember } from '@/modules/identity/api/identity.queries';
import { useToast } from '@/shared/components/ui/toast';

interface InviteMemberModalProps {
  profiles: ProfileDto[];
  onClose: () => void;
}

/**
 * Invite a person to the active company by e-mail, granting the chosen profile on accept
 * (card #107). On success the members list is invalidated by the mutation and the modal closes.
 */
export function InviteMemberModal({ profiles, onClose }: InviteMemberModalProps) {
  const invite = useInviteMember();
  const toast = useToast();
  const [email, setEmail] = useState('');
  const [profileId, setProfileId] = useState(profiles[0]?.id ?? '');

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    try {
      await invite.mutateAsync({ email: email.trim(), profileId });
      toast('success', 'Convite enviado com sucesso.');
      onClose();
    } catch (err) {
      toast('error', (err as ApiError)?.message ?? 'Não foi possível enviar o convite.');
    }
  }

  return (
    <Modal
      open
      onClose={onClose}
      title="Convidar membro"
      description="O convidado receberá um e-mail para entrar na empresa com o perfil escolhido."
      footer={
        <>
          <Button variant="outline" onClick={onClose} disabled={invite.isPending}>
            Cancelar
          </Button>
          <Button type="submit" form="invite-member-form" disabled={invite.isPending}>
            {invite.isPending && <Loader2 className="size-4 animate-spin" />}
            Enviar convite
          </Button>
        </>
      }
    >
      <form
        id="invite-member-form"
        className="flex flex-col gap-4"
        onSubmit={handleSubmit}
        noValidate
      >
        <div className="flex flex-col gap-2">
          <Label htmlFor="invite-email">E-mail</Label>
          <Input
            id="invite-email"
            type="email"
            autoComplete="off"
            placeholder="convidado@ufba.br"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            required
            autoFocus
          />
        </div>

        <div className="flex flex-col gap-2">
          <Label htmlFor="invite-profile">Perfil</Label>
          <select
            id="invite-profile"
            value={profileId}
            onChange={(e) => setProfileId(e.target.value)}
            required
            className="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-sm focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring disabled:cursor-not-allowed disabled:opacity-50"
          >
            {profiles.length === 0 ? (
              <option value="" disabled>
                Nenhum perfil disponível
              </option>
            ) : (
              profiles.map((profile) => (
                <option key={profile.id} value={profile.id}>
                  {profile.name}
                </option>
              ))
            )}
          </select>
        </div>

      </form>
    </Modal>
  );
}
