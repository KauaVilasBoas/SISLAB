import { useEffect, useMemo, useRef, useState, type FormEvent, type MouseEvent } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { ArrowLeft, ChevronDown, Loader2, ShieldCheck } from 'lucide-react';
import { useToast } from '@/shared/components/ui/toast';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import { Label } from '@/shared/components/ui/label';
import { Card, CardContent, CardHeader, CardTitle } from '@/shared/components/ui/card';
import { Badge } from '@/shared/components/ui/badge';
import { cn } from '@/shared/lib/utils';
import type { ApiError } from '@/shared/types/api';
import type { PermissionGroupDto } from '@/modules/identity/types';
import {
  useAvailablePermissions,
  useProfiles,
  useSetPermissions,
  useUpdateProfile,
} from '@/modules/identity/api/identity.queries';

// Checkbox that exposes the indeterminate state (not a real HTML attribute in React).
function IndeterminateCheckbox({
  checked,
  indeterminate,
  onChange,
  onClick,
}: {
  checked: boolean;
  indeterminate: boolean;
  onChange: () => void;
  onClick: (e: MouseEvent) => void;
}) {
  const ref = useRef<HTMLInputElement>(null);
  useEffect(() => {
    if (ref.current) ref.current.indeterminate = indeterminate;
  }, [indeterminate]);
  return (
    <input
      ref={ref}
      type="checkbox"
      className="size-4 shrink-0 cursor-pointer rounded border-input"
      checked={checked}
      onChange={onChange}
      onClick={onClick}
    />
  );
}

// One collapsible group of permissions. Starts open when any permission is pre-selected.
function PermissionAccordionGroup({
  group,
  selectedIds,
  initialOpen,
  onToggle,
  onToggleAll,
}: {
  group: PermissionGroupDto;
  selectedIds: Set<string>;
  initialOpen: boolean;
  onToggle: (id: string) => void;
  onToggleAll: (permissionIds: string[]) => void;
}) {
  const [isOpen, setIsOpen] = useState(initialOpen);
  const permissionIds = group.permissions.map((p) => p.id);
  const selectedInGroup = group.permissions.filter((p) => selectedIds.has(p.id)).length;
  const total = group.permissions.length;
  const allSelected = selectedInGroup === total;
  const someSelected = selectedInGroup > 0 && !allSelected;

  return (
    <div className="overflow-hidden rounded-lg border">
      {/* Accordion header — clicking anywhere in the row toggles, except the checkbox */}
      <button
        type="button"
        className="flex w-full items-center gap-3 px-4 py-3 text-left transition-colors hover:bg-muted/40"
        onClick={() => setIsOpen((v) => !v)}
      >
        <IndeterminateCheckbox
          checked={allSelected}
          indeterminate={someSelected}
          onChange={() => onToggleAll(permissionIds)}
          onClick={(e) => e.stopPropagation()}
        />
        <span className="flex-1 text-sm font-medium">{group.groupName}</span>
        <span
          className={cn(
            'rounded-full px-2 py-0.5 text-xs tabular-nums',
            selectedInGroup > 0
              ? 'bg-primary/10 font-medium text-primary'
              : 'text-muted-foreground',
          )}
        >
          {selectedInGroup}/{total}
        </span>
        <ChevronDown
          className={cn(
            'size-4 shrink-0 text-muted-foreground transition-transform duration-200',
            isOpen && 'rotate-180',
          )}
        />
      </button>

      {/* Permission checkboxes */}
      {isOpen && (
        <div className="border-t px-4 py-3">
          <div className="grid grid-cols-1 gap-1 sm:grid-cols-2">
            {group.permissions.map((permission) => (
              <label
                key={permission.id}
                className="flex cursor-pointer items-center gap-2 rounded px-2 py-1.5 text-sm transition-colors hover:bg-muted/40"
              >
                <input
                  type="checkbox"
                  className="size-4 shrink-0 rounded border-input"
                  checked={selectedIds.has(permission.id)}
                  onChange={() => onToggle(permission.id)}
                />
                <span>{permission.displayName}</span>
              </label>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}

export function ProfileEditPage() {
  const { profileId } = useParams<{ profileId: string }>();
  const navigate = useNavigate();

  const profiles = useProfiles();
  const profile = profiles.data?.find((p) => p.id === profileId);
  const isSystem = profile?.isSystem ?? false;

  const permissions = useAvailablePermissions(profileId, !!profileId && !isSystem);

  const updateProfile = useUpdateProfile();
  const setPermissions = useSetPermissions();
  const toast = useToast();

  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());

  // Seed form fields once profile loads from cache or network.
  useEffect(() => {
    if (profile) {
      setName(profile.name);
      setDescription(profile.description);
    }
  }, [profile]);

  // Seed checkbox selection from backend's pre-selected flags.
  useEffect(() => {
    if (!permissions.data) return;
    setSelectedIds(
      new Set(
        permissions.data.flatMap((g) =>
          g.permissions.filter((p) => p.selected).map((p) => p.id),
        ),
      ),
    );
  }, [permissions.data]);

  const totalPermissions = useMemo(
    () => permissions.data?.reduce((sum, g) => sum + g.permissions.length, 0) ?? 0,
    [permissions.data],
  );

  function toggle(id: string) {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }

  function toggleAll(permissionIds: string[]) {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      const allIn = permissionIds.every((id) => next.has(id));
      if (allIn) {
        permissionIds.forEach((id) => next.delete(id));
      } else {
        permissionIds.forEach((id) => next.add(id));
      }
      return next;
    });
  }

  const saving = updateProfile.isPending || setPermissions.isPending;

  async function handleSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    if (!profileId) return;
    try {
      await updateProfile.mutateAsync({
        profileId,
        body: { name: name.trim(), description: description.trim() },
      });
      if (!isSystem) {
        await setPermissions.mutateAsync({
          profileId,
          permissionIds: [...selectedIds],
        });
      }
      toast('success', 'Perfil salvo com sucesso.');
    } catch (err) {
      toast('error', (err as ApiError)?.message ?? 'Não foi possível salvar o perfil.');
    }
  }

  function goBack() {
    navigate('/members', { state: { tab: 'profiles' } });
  }

  // ── Loading ──────────────────────────────────────────────────────────────────
  if (profiles.isLoading) {
    return (
      <div className="flex h-64 items-center justify-center gap-2 text-muted-foreground">
        <Loader2 className="size-5 animate-spin" />
        <span className="text-sm">Carregando perfil…</span>
      </div>
    );
  }

  // ── Not found ────────────────────────────────────────────────────────────────
  if (!profile) {
    return (
      <div className="space-y-4">
        <button
          type="button"
          onClick={goBack}
          className="flex items-center gap-1.5 text-sm text-muted-foreground transition-colors hover:text-foreground"
        >
          <ArrowLeft className="size-4" />
          Voltar
        </button>
        <p className="text-sm text-muted-foreground">Perfil não encontrado.</p>
      </div>
    );
  }

  // ── Main render ──────────────────────────────────────────────────────────────
  return (
    <form className="space-y-6" onSubmit={handleSubmit} noValidate>
      {/* Page header */}
      <div className="flex flex-col gap-1 border-b pb-4 sm:flex-row sm:items-start sm:justify-between">
        <div className="flex flex-col gap-1">
          <button
            type="button"
            onClick={goBack}
            className="flex w-fit items-center gap-1 text-sm text-muted-foreground transition-colors hover:text-foreground"
          >
            <ArrowLeft className="size-3.5" />
            Membros &amp; Perfis
          </button>
          <h1 className="text-2xl font-semibold tracking-tight">{profile.name}</h1>
          {isSystem && (
            <Badge variant="muted" className="w-fit">
              <ShieldCheck className="size-3" />
              Perfil de sistema
            </Badge>
          )}
        </div>

        {!isSystem && (
          <div className="flex shrink-0 items-center gap-2 pt-1">
            <Button type="button" variant="outline" onClick={goBack} disabled={saving}>
              Cancelar
            </Button>
            <Button type="submit" disabled={saving}>
              {saving && <Loader2 className="size-4 animate-spin" />}
              Salvar
            </Button>
          </div>
        )}
      </div>

      {/* Info card */}
      <Card>
        <CardHeader className="pb-4">
          <CardTitle className="text-base">Informações</CardTitle>
        </CardHeader>
        <CardContent className="flex flex-col gap-4">
          <div className="flex flex-col gap-2">
            <Label htmlFor="profile-name">Nome</Label>
            <Input
              id="profile-name"
              value={name}
              onChange={(e) => setName(e.target.value)}
              required
              disabled={isSystem}
            />
          </div>
          <div className="flex flex-col gap-2">
            <Label htmlFor="profile-description">Descrição</Label>
            <Input
              id="profile-description"
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              placeholder="Opcional"
              disabled={isSystem}
            />
          </div>
          {isSystem && (
            <p className="text-xs text-muted-foreground">
              Perfis de sistema são gerenciados automaticamente e não podem ser editados.
            </p>
          )}
        </CardContent>
      </Card>

      {/* Permissions card — hidden for system profiles */}
      {!isSystem && (
        <Card>
          <CardHeader className="flex-row items-center justify-between space-y-0 pb-4">
            <CardTitle className="text-base">Permissões</CardTitle>
            {permissions.data && (
              <span className="text-xs text-muted-foreground">
                {selectedIds.size} de {totalPermissions} selecionadas
              </span>
            )}
          </CardHeader>
          <CardContent className="flex flex-col gap-3">
            {permissions.isLoading ? (
              <div className="flex items-center gap-2 text-sm text-muted-foreground">
                <Loader2 className="size-4 animate-spin" />
                Carregando permissões…
              </div>
            ) : permissions.isError ? (
              <p className="text-sm text-destructive">
                Não foi possível carregar as permissões.
              </p>
            ) : permissions.data && permissions.data.length > 0 ? (
              <>
                {permissions.data.map((group) => (
                  <PermissionAccordionGroup
                    key={group.groupId ?? 'ungrouped'}
                    group={group}
                    selectedIds={selectedIds}
                    initialOpen={group.permissions.some((p) => p.selected)}
                    onToggle={toggle}
                    onToggleAll={toggleAll}
                  />
                ))}

                {/* Sticky save at the bottom of the permissions section */}
                <div className="flex items-center justify-between border-t pt-4">
                  <span className="text-xs text-muted-foreground">
                    {selectedIds.size} de {totalPermissions} selecionadas
                  </span>
                  <Button type="submit" disabled={saving}>
                    {saving && <Loader2 className="size-4 animate-spin" />}
                    Salvar permissões
                  </Button>
                </div>
              </>
            ) : (
              <p className="text-sm text-muted-foreground">
                Nenhuma permissão disponível no sistema.
              </p>
            )}
          </CardContent>
        </Card>
      )}
    </form>
  );
}
