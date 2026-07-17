import { Loader2, Plus, X } from 'lucide-react';
import { Modal } from '@/shared/components/ui/modal';
import { Button } from '@/shared/components/ui/button';
import { Badge } from '@/shared/components/ui/badge';
import type { ApiError } from '@/shared/types/api';
import { RequirePermission } from '@/modules/auth/PermissionsProvider';
import { Permissions } from '@/modules/auth/permissions';
import type { EnrichedMemberDto, ProfileDto } from '@/modules/identity/types';
import {
  useAssignProfile,
  useMembers,
  useRemoveProfile,
} from '@/modules/identity/api/identity.queries';
import { useToast } from '@/shared/components/ui/toast';

interface ManageMemberProfilesModalProps {
  member: EnrichedMemberDto;
  profiles: ProfileDto[];
  onClose: () => void;
}

/**
 * Assign/remove authorization profiles for a single member. Subscribes to the members query
 * so assigned-profile chips update in real time after each mutation (the mutation invalidates
 * the same cache key, triggering a refetch that the modal picks up immediately).
 */
export function ManageMemberProfilesModal({
  member,
  profiles,
  onClose,
}: ManageMemberProfilesModalProps) {
  const assign = useAssignProfile();
  const remove = useRemoveProfile();
  const toast = useToast();

  // Subscribe to live member data — mutations invalidate this same key, so the modal
  // re-renders with the updated assignedProfiles list without any extra network call
  // (the cache is already warm from the parent's MembersTab).
  const members = useMembers();
  const liveMember = members.data?.find((m) => m.userId === member.userId) ?? member;

  const assignedIds = new Set(liveMember.assignedProfiles.map((p) => p.profileId));
  const assignable = profiles.filter((p) => !assignedIds.has(p.id));
  const pending = assign.isPending || remove.isPending;

  async function handleAssign(profileId: string) {
    try {
      await assign.mutateAsync({ userId: member.userId, profileId });
      toast('success', 'Perfil atribuído.');
    } catch (err) {
      toast('error', (err as ApiError)?.message ?? 'Não foi possível atribuir o perfil.');
    }
  }

  async function handleRemove(profileId: string) {
    try {
      await remove.mutateAsync({ userId: member.userId, profileId });
      toast('success', 'Perfil removido.');
    } catch (err) {
      toast('error', (err as ApiError)?.message ?? 'Não foi possível remover o perfil.');
    }
  }

  return (
    <Modal
      open
      onClose={onClose}
      title="Gerenciar perfis"
      description={`${liveMember.username} · ${liveMember.email}`}
      footer={
        <Button variant="outline" onClick={onClose}>
          Fechar
        </Button>
      }
    >
      <div className="space-y-6">
        <section className="space-y-2">
          <h3 className="text-sm font-medium">Perfis atribuídos</h3>
          {liveMember.assignedProfiles.length === 0 ? (
            <p className="text-sm text-muted-foreground">
              Este membro ainda não possui perfis.
            </p>
          ) : (
            <ul className="flex flex-wrap gap-2">
              {liveMember.assignedProfiles.map((profile) => (
                <li key={profile.profileId}>
                  <Badge variant="secondary" className="pr-1">
                    {profile.profileName}
                    <RequirePermission code={Permissions.profiles.removeProfile}>
                      <button
                        type="button"
                        aria-label={`Remover ${profile.profileName}`}
                        disabled={pending}
                        onClick={() => handleRemove(profile.profileId)}
                        className="ml-1 rounded-full p-0.5 hover:bg-background/60 disabled:opacity-50"
                      >
                        <X className="size-3" />
                      </button>
                    </RequirePermission>
                  </Badge>
                </li>
              ))}
            </ul>
          )}
        </section>

        <RequirePermission code={Permissions.profiles.assignProfile}>
          <section className="space-y-2">
            <h3 className="text-sm font-medium">Adicionar perfil</h3>
            {assignable.length === 0 ? (
              <p className="text-sm text-muted-foreground">
                Todos os perfis já foram atribuídos.
              </p>
            ) : (
              <ul className="flex flex-col divide-y rounded-lg border">
                {assignable.map((profile) => (
                  <li
                    key={profile.id}
                    className="flex items-center justify-between gap-3 px-3 py-2"
                  >
                    <div className="min-w-0">
                      <p className="truncate text-sm font-medium">{profile.name}</p>
                      {profile.description ? (
                        <p className="truncate text-xs text-muted-foreground">
                          {profile.description}
                        </p>
                      ) : null}
                    </div>
                    <Button
                      variant="outline"
                      size="sm"
                      disabled={pending}
                      onClick={() => handleAssign(profile.id)}
                    >
                      <Plus className="size-3.5" />
                      Adicionar
                    </Button>
                  </li>
                ))}
              </ul>
            )}
          </section>
        </RequirePermission>

        {pending && (
          <p className="flex items-center gap-2 text-sm text-muted-foreground">
            <Loader2 className="size-4 animate-spin" />
            Salvando…
          </p>
        )}
      </div>
    </Modal>
  );
}
