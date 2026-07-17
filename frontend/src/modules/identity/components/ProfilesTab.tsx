import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Loader2, Pencil, Plus, ShieldCheck } from 'lucide-react';
import { Button } from '@/shared/components/ui/button';
import { Card, CardContent } from '@/shared/components/ui/card';
import { Badge } from '@/shared/components/ui/badge';
import { RequirePermission } from '@/modules/auth/PermissionsProvider';
import { Permissions } from '@/modules/auth/permissions';
import type { ProfileDto } from '@/modules/identity/types';
import { useProfiles } from '@/modules/identity/api/identity.queries';
import { ProfileEditorModal } from '@/modules/identity/components/ProfileEditorModal';

/** "Profiles" tab: lists profiles with create/edit; editing navigates to a dedicated page. */
export function ProfilesTab() {
  const navigate = useNavigate();
  const profiles = useProfiles();
  const [createOpen, setCreateOpen] = useState(false);

  function openEdit(profile: ProfileDto) {
    navigate(`/members/profiles/${profile.id}`);
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-end">
        <RequirePermission code={Permissions.profiles.createProfile}>
          <Button onClick={() => setCreateOpen(true)}>
            <Plus className="size-4" />
            Novo perfil
          </Button>
        </RequirePermission>
      </div>

      {profiles.isLoading ? (
        <Card>
          <CardContent className="flex items-center justify-center gap-2 py-16 text-sm text-muted-foreground">
            <Loader2 className="size-4 animate-spin" />
            Carregando perfis…
          </CardContent>
        </Card>
      ) : profiles.isError ? (
        <Card>
          <CardContent className="py-16 text-center text-sm text-destructive">
            Não foi possível carregar os perfis.
          </CardContent>
        </Card>
      ) : profiles.data && profiles.data.length > 0 ? (
        <ul className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
          {profiles.data.map((profile) => (
            <li key={profile.id}>
              <Card className="flex h-full flex-col">
                <CardContent className="flex flex-1 flex-col gap-3 p-5">
                  <div className="flex items-start justify-between gap-2">
                    <h3 className="font-medium">{profile.name}</h3>
                    {profile.isSystem ? (
                      <Badge variant="muted">
                        <ShieldCheck className="size-3" />
                        Sistema
                      </Badge>
                    ) : null}
                  </div>
                  <p className="flex-1 text-sm text-muted-foreground">
                    {profile.description || 'Sem descrição.'}
                  </p>
                  <div className="flex justify-end">
                    <Button variant="outline" size="sm" onClick={() => openEdit(profile)}>
                      <Pencil className="size-3.5" />
                      {profile.isSystem ? 'Ver' : 'Editar'}
                    </Button>
                  </div>
                </CardContent>
              </Card>
            </li>
          ))}
        </ul>
      ) : (
        <Card>
          <CardContent className="flex flex-col items-center justify-center gap-3 py-16 text-center">
            <ShieldCheck className="size-8 text-muted-foreground" />
            <p className="text-sm text-muted-foreground">
              Nenhum perfil ainda. Crie o primeiro perfil de permissões.
            </p>
          </CardContent>
        </Card>
      )}

      {createOpen ? (
        <ProfileEditorModal onClose={() => setCreateOpen(false)} />
      ) : null}
    </div>
  );
}
