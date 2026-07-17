import { useState } from 'react';
import { Loader2, Settings2, UserPlus, Users } from 'lucide-react';
import { Button } from '@/shared/components/ui/button';
import { Card, CardContent } from '@/shared/components/ui/card';
import { Badge } from '@/shared/components/ui/badge';
import {
  RequireAnyPermission,
  RequirePermission,
} from '@/modules/auth/PermissionsProvider';
import { Permissions } from '@/modules/auth/permissions';
import type { EnrichedMemberDto, ProfileDto } from '@/modules/identity/types';
import { useMembers } from '@/modules/identity/api/identity.queries';
import { InviteMemberModal } from '@/modules/identity/components/InviteMemberModal';
import { ManageMemberProfilesModal } from '@/modules/identity/components/ManageMemberProfilesModal';

interface MembersTabProps {
  profiles: ProfileDto[];
}

/** "Members" tab: the enriched member table plus invite and per-member profile management. */
export function MembersTab({ profiles }: MembersTabProps) {
  const members = useMembers();
  const [inviting, setInviting] = useState(false);
  const [managing, setManaging] = useState<EnrichedMemberDto | null>(null);

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-end">
        <RequirePermission code={Permissions.members.invite}>
          <Button onClick={() => setInviting(true)}>
            <UserPlus className="size-4" />
            Convidar membro
          </Button>
        </RequirePermission>
      </div>

      {members.isLoading ? (
        <Card>
          <CardContent className="flex items-center justify-center gap-2 py-16 text-sm text-muted-foreground">
            <Loader2 className="size-4 animate-spin" />
            Carregando membros…
          </CardContent>
        </Card>
      ) : members.isError ? (
        <Card>
          <CardContent className="py-16 text-center text-sm text-destructive">
            Não foi possível carregar os membros.
          </CardContent>
        </Card>
      ) : members.data && members.data.length > 0 ? (
        <Card>
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b text-left text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                  <th className="px-4 py-3">Membro</th>
                  <th className="px-4 py-3">Perfis</th>
                  <th className="px-4 py-3 text-right">Ações</th>
                </tr>
              </thead>
              <tbody>
                {members.data.map((member) => (
                  <tr key={member.membershipId} className="border-b last:border-0">
                    <td className="px-4 py-3">
                      <div className="font-medium">{member.username}</div>
                      <div className="text-xs text-muted-foreground">{member.email}</div>
                    </td>
                    <td className="px-4 py-3">
                      {member.assignedProfiles.length === 0 ? (
                        <span className="text-xs text-muted-foreground">Sem perfis</span>
                      ) : (
                        <div className="flex flex-wrap gap-1.5">
                          {member.assignedProfiles.map((profile) => (
                            <Badge key={profile.profileId} variant="secondary">
                              {profile.profileName}
                            </Badge>
                          ))}
                        </div>
                      )}
                    </td>
                    <td className="px-4 py-3 text-right">
                      <RequireAnyPermission
                        codes={[
                          Permissions.profiles.assignProfile,
                          Permissions.profiles.removeProfile,
                        ]}
                        fallback={<span className="text-xs text-muted-foreground">—</span>}
                      >
                        <Button
                          variant="outline"
                          size="sm"
                          onClick={() => setManaging(member)}
                        >
                          <Settings2 className="size-3.5" />
                          Gerenciar perfis
                        </Button>
                      </RequireAnyPermission>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </Card>
      ) : (
        <Card>
          <CardContent className="flex flex-col items-center justify-center gap-3 py-16 text-center">
            <Users className="size-8 text-muted-foreground" />
            <p className="text-sm text-muted-foreground">
              Nenhum membro ainda. Convide a primeira pessoa para a empresa.
            </p>
          </CardContent>
        </Card>
      )}

      {inviting ? (
        <InviteMemberModal profiles={profiles} onClose={() => setInviting(false)} />
      ) : null}

      {managing ? (
        <ManageMemberProfilesModal
          member={managing}
          profiles={profiles}
          onClose={() => setManaging(null)}
        />
      ) : null}
    </div>
  );
}
