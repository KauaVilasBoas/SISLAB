import { useMemo, useState, type FormEvent } from 'react';
import {
  CheckCircle2,
  ChevronDown,
  ChevronRight,
  ClipboardList,
  Loader2,
  Plus,
  Trash2,
  UserPlus,
} from 'lucide-react';
import { Button } from '@/shared/components/ui/button';
import { Badge } from '@/shared/components/ui/badge';
import { Modal } from '@/shared/components/ui/modal';
import { Input } from '@/shared/components/ui/input';
import { Label } from '@/shared/components/ui/label';
import { SingleSelect } from '@/shared/components/ui/single-select';
import { useToast } from '@/shared/components/ui/toast';
import { cn } from '@/shared/lib/utils';
import type { ApiError } from '@/shared/types/api';
import { RequirePermission } from '@/modules/auth/PermissionsProvider';
import { Permissions } from '@/modules/auth/permissions';
import { useRooms, useCollectionRoles } from '@/modules/configuration/api/configuration.queries';
import { useMembers } from '@/modules/identity/api/identity.queries';
import { sampleTypeLabel } from '@/modules/in-vivo/presentation';
import {
  useAssignCollectionRole,
  useCollectionPlan,
  useCollectionStatusBoard,
  useCreateCollectionPlan,
  useDefineSampleRouting,
  useRemoveCollectionRole,
  useRemoveSampleRouting,
} from '@/modules/in-vivo/api/collection.queries';
import type {
  CollectionPlanView,
  SampleRoutingView,
  SampleType,
} from '@/modules/in-vivo/types';

/** The sample types offered when routing the matrix, mirroring the backend SampleType enum (persisted by name). */
const SAMPLE_TYPE_OPTIONS: SampleType[] = [
  'Blood',
  'Plasma',
  'Serum',
  'Tissue',
  'CerebrospinalFluid',
  'Urine',
  'Other',
];

interface CollectionPlanPanelProps {
  projectId: string;
  batchId: string;
}

/**
 * Collection plan of a batch (SISLAB-08): the matrix (sample type → planned analyses + storage), the role roster
 * (collection role ↔ member) and the derived status board (pending/done per analysis, from the real biobank). A
 * collapsible panel that mirrors the paper collection sheet. While no plan exists the panel offers to create one;
 * afterwards it exposes the define/remove-routing and assign/remove-role actions, each permission-gated.
 */
export function CollectionPlanPanel({ projectId, batchId }: CollectionPlanPanelProps) {
  const [open, setOpen] = useState(false);
  const plan = useCollectionPlan(batchId, open);
  const board = useCollectionStatusBoard(batchId, open && Boolean(plan.data));

  const create = useCreateCollectionPlan(batchId);
  const toast = useToast();

  const hasPlan = Boolean(plan.data);
  const planMissing = plan.isError && !plan.isLoading; // 404 → no plan yet (retry disabled)

  async function handleCreate() {
    try {
      await create.mutateAsync({ projectId, batchId });
      toast('success', 'Plano de coleta criado.');
    } catch (err) {
      toast('error', (err as ApiError)?.message ?? 'Não foi possível criar o plano.');
    }
  }

  return (
    <div className="border-t">
      <button
        type="button"
        onClick={() => setOpen((value) => !value)}
        className="flex w-full items-center justify-between gap-3 px-4 py-3 text-left"
        aria-expanded={open}
      >
        <span className="flex items-center gap-2 text-sm font-medium">
          <ClipboardList className="size-4 text-muted-foreground" />
          Plano de coleta
        </span>
        {open ? (
          <ChevronDown className="size-4 text-muted-foreground" />
        ) : (
          <ChevronRight className="size-4 text-muted-foreground" />
        )}
      </button>

      {open && (
        <div className="space-y-6 px-4 pb-4">
          {plan.isLoading ? (
            <div className="flex items-center gap-2 p-4 text-sm text-muted-foreground">
              <Loader2 className="size-4 animate-spin" />
              Carregando plano de coleta…
            </div>
          ) : planMissing ? (
            <div className="flex flex-col items-center gap-3 rounded-md border border-dashed p-6 text-center">
              <p className="text-sm text-muted-foreground">
                Nenhum plano de coleta para esta leva ainda.
              </p>
              <RequirePermission code={Permissions.collectionPlans.create}>
                <Button size="sm" onClick={handleCreate} disabled={create.isPending}>
                  {create.isPending ? (
                    <Loader2 className="size-4 animate-spin" />
                  ) : (
                    <Plus className="size-4" />
                  )}
                  Criar plano de coleta
                </Button>
              </RequirePermission>
            </div>
          ) : hasPlan && plan.data ? (
            <>
              <RoutingMatrix plan={plan.data} batchId={batchId} />
              <RoleRoster plan={plan.data} batchId={batchId} />
              <StatusBoard
                loading={board.isLoading}
                error={board.isError}
                rows={board.data?.rows ?? []}
              />
            </>
          ) : null}
        </div>
      )}
    </div>
  );
}

// ---------------------------------------------------------------------------
// Matrix: sample type → planned analyses + storage
// ---------------------------------------------------------------------------

function RoutingMatrix({ plan, batchId }: { plan: CollectionPlanView; batchId: string }) {
  const [defining, setDefining] = useState<SampleRoutingView | null>(null);
  const [creatingRouting, setCreatingRouting] = useState(false);
  const rooms = useRooms();
  const removeRouting = useRemoveSampleRouting(plan.id, batchId);
  const toast = useToast();

  const roomNameById = useMemo(
    () => new Map((rooms.data ?? []).map((room) => [room.id, room.name])),
    [rooms.data],
  );

  async function handleRemove(sampleType: string) {
    try {
      await removeRouting.mutateAsync(sampleType);
      toast('success', 'Roteamento removido.');
    } catch (err) {
      toast('error', (err as ApiError)?.message ?? 'Não foi possível remover o roteamento.');
    }
  }

  return (
    <section className="space-y-3">
      <div className="flex items-center justify-between">
        <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
          Matriz amostra → análises
        </p>
        <RequirePermission code={Permissions.collectionPlans.defineRouting}>
          <Button variant="outline" size="sm" onClick={() => setCreatingRouting(true)}>
            <Plus className="size-4" />
            Tipo de amostra
          </Button>
        </RequirePermission>
      </div>

      {plan.routings.length === 0 ? (
        <p className="rounded-md border border-dashed p-4 text-center text-sm text-muted-foreground">
          Nenhum roteamento. Defina, por tipo de amostra, quais análises e onde armazenar.
        </p>
      ) : (
        <div className="overflow-hidden rounded-lg border">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b bg-muted/30 text-left text-xs uppercase tracking-wide text-muted-foreground">
                <th className="px-4 py-2.5 font-medium">Amostra</th>
                <th className="px-4 py-2.5 font-medium">Análises previstas</th>
                <th className="px-4 py-2.5 font-medium">Armazenamento</th>
                <th className="px-4 py-2.5" />
              </tr>
            </thead>
            <tbody>
              {plan.routings.map((routing) => {
                const conservation =
                  routing.conservationTempMinCelsius != null ||
                  routing.conservationTempMaxCelsius != null
                    ? `${routing.conservationTempMinCelsius ?? '?'} °C a ${
                        routing.conservationTempMaxCelsius ?? '?'
                      } °C`
                    : null;
                const room = routing.storageRoomId
                  ? roomNameById.get(routing.storageRoomId) ?? 'Sala'
                  : null;
                return (
                  <tr key={routing.sampleType} className="border-b last:border-0 align-top">
                    <td className="px-4 py-3 font-medium">
                      {sampleTypeLabel[routing.sampleType as SampleType] ?? routing.sampleType}
                    </td>
                    <td className="px-4 py-3">
                      {routing.plannedAnalyses.length > 0 ? (
                        <div className="flex flex-wrap gap-1.5">
                          {routing.plannedAnalyses.map((name) => (
                            <Badge key={name} variant="muted">
                              {name}
                            </Badge>
                          ))}
                        </div>
                      ) : (
                        <span className="text-muted-foreground">—</span>
                      )}
                    </td>
                    <td className="px-4 py-3 text-muted-foreground">
                      <div className="space-y-0.5">
                        <p>{routing.storageLabel ?? room ?? '—'}</p>
                        {room && routing.storageLabel ? (
                          <p className="text-xs">{room}</p>
                        ) : null}
                        {conservation ? <p className="text-xs">{conservation}</p> : null}
                      </div>
                    </td>
                    <td className="px-4 py-3 text-right">
                      <div className="flex justify-end gap-1">
                        <RequirePermission code={Permissions.collectionPlans.defineRouting}>
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={() => setDefining(routing)}
                          >
                            Editar
                          </Button>
                        </RequirePermission>
                        <RequirePermission code={Permissions.collectionPlans.removeRouting}>
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={() => handleRemove(routing.sampleType)}
                            disabled={removeRouting.isPending}
                          >
                            <Trash2 className="size-4" />
                          </Button>
                        </RequirePermission>
                      </div>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}

      {(creatingRouting || defining) && (
        <DefineRoutingModal
          planId={plan.id}
          batchId={batchId}
          existing={defining}
          usedTypes={plan.routings.map((r) => r.sampleType)}
          onClose={() => {
            setCreatingRouting(false);
            setDefining(null);
          }}
        />
      )}
    </section>
  );
}

function DefineRoutingModal({
  planId,
  batchId,
  existing,
  usedTypes,
  onClose,
}: {
  planId: string;
  batchId: string;
  existing: SampleRoutingView | null;
  usedTypes: string[];
  onClose: () => void;
}) {
  const define = useDefineSampleRouting(planId, batchId);
  const rooms = useRooms();
  const toast = useToast();

  const [sampleType, setSampleType] = useState<string | null>(existing?.sampleType ?? null);
  const [analysesText, setAnalysesText] = useState(
    (existing?.plannedAnalyses ?? []).join(', '),
  );
  const [storageRoomId, setStorageRoomId] = useState<string | null>(
    existing?.storageRoomId ?? null,
  );
  const [storageLabel, setStorageLabel] = useState(existing?.storageLabel ?? '');
  const [tempMin, setTempMin] = useState(
    existing?.conservationTempMinCelsius?.toString() ?? '',
  );
  const [tempMax, setTempMax] = useState(
    existing?.conservationTempMaxCelsius?.toString() ?? '',
  );

  // When editing an existing routing its type is fixed; when creating, hide already-routed types.
  const typeOptions = SAMPLE_TYPE_OPTIONS.filter(
    (type) => type === existing?.sampleType || !usedTypes.includes(type),
  ).map((type) => ({ value: type, label: sampleTypeLabel[type] }));

  const roomOptions = (rooms.data ?? []).map((room) => ({ value: room.id, label: room.name }));

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!sampleType) {
      toast('error', 'Escolha o tipo de amostra.');
      return;
    }

    const plannedAnalyses = analysesText
      .split(',')
      .map((name) => name.trim())
      .filter(Boolean);
    if (plannedAnalyses.length === 0) {
      toast('error', 'Informe ao menos uma análise prevista.');
      return;
    }

    const parsedMin = tempMin.trim() ? Number(tempMin.trim()) : null;
    const parsedMax = tempMax.trim() ? Number(tempMax.trim()) : null;
    if (
      (parsedMin != null && !Number.isFinite(parsedMin)) ||
      (parsedMax != null && !Number.isFinite(parsedMax))
    ) {
      toast('error', 'Informe temperaturas numéricas válidas.');
      return;
    }

    try {
      await define.mutateAsync({
        sampleType: sampleType as SampleType,
        plannedAnalyses,
        storageRoomId: storageRoomId,
        storageLabel: storageLabel.trim() || null,
        conservationTempMinCelsius: parsedMin,
        conservationTempMaxCelsius: parsedMax,
      });
      toast('success', 'Roteamento definido.');
      onClose();
    } catch (err) {
      toast('error', (err as ApiError)?.message ?? 'Não foi possível definir o roteamento.');
    }
  }

  return (
    <Modal
      open
      onClose={onClose}
      title={existing ? 'Editar roteamento' : 'Novo roteamento de amostra'}
      description="Defina, para um tipo de amostra, as análises previstas e onde armazenar."
      footer={
        <>
          <Button variant="outline" onClick={onClose} disabled={define.isPending}>
            Cancelar
          </Button>
          <Button type="submit" form="define-routing-form" disabled={define.isPending}>
            {define.isPending && <Loader2 className="size-4 animate-spin" />}
            Salvar
          </Button>
        </>
      }
    >
      <form
        id="define-routing-form"
        className="flex flex-col gap-4"
        onSubmit={handleSubmit}
        noValidate
      >
        <div className="flex flex-col gap-2">
          <Label>Tipo de amostra</Label>
          <SingleSelect
            label="Tipo de amostra"
            placeholder="Selecionar amostra…"
            options={typeOptions}
            value={sampleType}
            onChange={setSampleType}
            disabled={Boolean(existing)}
          />
        </div>
        <div className="flex flex-col gap-2">
          <Label htmlFor="routing-analyses">Análises previstas</Label>
          <Input
            id="routing-analyses"
            placeholder="Hemograma, Bioquímica"
            value={analysesText}
            onChange={(e) => setAnalysesText(e.target.value)}
            required
          />
          <p className="text-xs text-muted-foreground">Separe as análises por vírgula.</p>
        </div>
        <div className="flex flex-col gap-2">
          <Label htmlFor="routing-storage-label">Rótulo de armazenamento (opcional)</Label>
          <Input
            id="routing-storage-label"
            placeholder="Fiocruz / −20 °C"
            value={storageLabel}
            onChange={(e) => setStorageLabel(e.target.value)}
          />
        </div>
        <div className="flex flex-col gap-2">
          <Label>Sala de armazenamento (opcional)</Label>
          <SingleSelect
            label="Sala de armazenamento"
            placeholder="Selecionar sala…"
            options={roomOptions}
            value={storageRoomId}
            onChange={setStorageRoomId}
          />
        </div>
        <div className="grid grid-cols-2 gap-4">
          <div className="flex flex-col gap-2">
            <Label htmlFor="routing-temp-min">Temp. mín. (°C)</Label>
            <Input
              id="routing-temp-min"
              type="number"
              step="any"
              inputMode="decimal"
              placeholder="-80"
              value={tempMin}
              onChange={(e) => setTempMin(e.target.value)}
            />
          </div>
          <div className="flex flex-col gap-2">
            <Label htmlFor="routing-temp-max">Temp. máx. (°C)</Label>
            <Input
              id="routing-temp-max"
              type="number"
              step="any"
              inputMode="decimal"
              placeholder="-20"
              value={tempMax}
              onChange={(e) => setTempMax(e.target.value)}
            />
          </div>
        </div>
      </form>
    </Modal>
  );
}

// ---------------------------------------------------------------------------
// Roster: collection role ↔ member
// ---------------------------------------------------------------------------

function RoleRoster({ plan, batchId }: { plan: CollectionPlanView; batchId: string }) {
  const [assigning, setAssigning] = useState(false);
  const roles = useCollectionRoles();
  const members = useMembers();
  const removeRole = useRemoveCollectionRole(plan.id, batchId);
  const toast = useToast();

  const roleNameById = useMemo(
    () => new Map((roles.data ?? []).map((role) => [role.id, role.name])),
    [roles.data],
  );
  const memberNameById = useMemo(
    () => new Map((members.data ?? []).map((member) => [member.userId, member.username])),
    [members.data],
  );

  async function handleRemove(roleId: string) {
    try {
      await removeRole.mutateAsync(roleId);
      toast('success', 'Atribuição removida.');
    } catch (err) {
      toast('error', (err as ApiError)?.message ?? 'Não foi possível remover a atribuição.');
    }
  }

  return (
    <section className="space-y-3">
      <div className="flex items-center justify-between">
        <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
          Funções da coleta
        </p>
        <RequirePermission code={Permissions.collectionPlans.assignRole}>
          <Button variant="outline" size="sm" onClick={() => setAssigning(true)}>
            <UserPlus className="size-4" />
            Atribuir função
          </Button>
        </RequirePermission>
      </div>

      {plan.assignments.length === 0 ? (
        <p className="rounded-md border border-dashed p-4 text-center text-sm text-muted-foreground">
          Nenhuma função atribuída. Vincule cada tarefa da coleta a um membro.
        </p>
      ) : (
        <div className="flex flex-wrap gap-2">
          {plan.assignments.map((assignment) => (
            <div
              key={assignment.roleId}
              className="flex items-center gap-2 rounded-lg border bg-muted/30 px-3 py-2"
            >
              <div className="min-w-0">
                <span className="block truncate text-sm font-medium">
                  {roleNameById.get(assignment.roleId) ?? 'Função'}
                </span>
                <span className="block text-xs text-muted-foreground">
                  {memberNameById.get(assignment.userId) ?? assignment.userId}
                </span>
              </div>
              <RequirePermission code={Permissions.collectionPlans.removeRole}>
                <Button
                  variant="ghost"
                  size="sm"
                  onClick={() => handleRemove(assignment.roleId)}
                  disabled={removeRole.isPending}
                >
                  <Trash2 className="size-4" />
                </Button>
              </RequirePermission>
            </div>
          ))}
        </div>
      )}

      {assigning && (
        <AssignRoleModal planId={plan.id} batchId={batchId} onClose={() => setAssigning(false)} />
      )}
    </section>
  );
}

function AssignRoleModal({
  planId,
  batchId,
  onClose,
}: {
  planId: string;
  batchId: string;
  onClose: () => void;
}) {
  const assign = useAssignCollectionRole(planId, batchId);
  const roles = useCollectionRoles();
  const members = useMembers();
  const toast = useToast();

  const [roleId, setRoleId] = useState<string | null>(null);
  const [userId, setUserId] = useState<string | null>(null);

  const roleOptions = (roles.data ?? []).map((role) => ({
    value: role.id,
    label: role.name,
    hint: role.description ?? undefined,
  }));
  const memberOptions = (members.data ?? []).map((member) => ({
    value: member.userId,
    label: member.username,
    hint: member.email,
  }));

  async function handleConfirm() {
    if (!roleId || !userId) {
      toast('error', 'Escolha a função e o membro.');
      return;
    }
    try {
      await assign.mutateAsync({ roleId, userId });
      toast('success', 'Função atribuída ao membro.');
      onClose();
    } catch (err) {
      toast('error', (err as ApiError)?.message ?? 'Não foi possível atribuir a função.');
    }
  }

  return (
    <Modal
      open
      onClose={onClose}
      title="Atribuir função de coleta"
      description="Vincule uma função da coleta a um membro da empresa."
      footer={
        <>
          <Button variant="outline" onClick={onClose} disabled={assign.isPending}>
            Cancelar
          </Button>
          <Button onClick={handleConfirm} disabled={assign.isPending || !roleId || !userId}>
            {assign.isPending && <Loader2 className="size-4 animate-spin" />}
            Atribuir
          </Button>
        </>
      }
    >
      <div className="flex flex-col gap-4">
        <div className="flex flex-col gap-2">
          <Label>Função</Label>
          {roleOptions.length === 0 ? (
            <p className="text-sm text-muted-foreground">
              Nenhuma função cadastrada. Crie em Configurações → Funções de coleta.
            </p>
          ) : (
            <SingleSelect
              label="Função"
              placeholder="Selecionar função…"
              options={roleOptions}
              value={roleId}
              onChange={setRoleId}
            />
          )}
        </div>
        <div className="flex flex-col gap-2">
          <Label>Membro</Label>
          <SingleSelect
            label="Membro"
            placeholder="Selecionar membro…"
            options={memberOptions}
            value={userId}
            onChange={setUserId}
          />
        </div>
      </div>
    </Modal>
  );
}

// ---------------------------------------------------------------------------
// Status board (derived from the real biobank analyses)
// ---------------------------------------------------------------------------

function StatusBoard({
  loading,
  error,
  rows,
}: {
  loading: boolean;
  error: boolean;
  rows: import('@/modules/in-vivo/types').CollectionStatusRow[];
}) {
  const members = useMembers();
  const memberNameById = useMemo(
    () => new Map((members.data ?? []).map((member) => [member.userId, member.username])),
    [members.data],
  );

  return (
    <section className="space-y-3">
      <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
        Quadro de status
      </p>

      {loading ? (
        <div className="flex items-center gap-2 p-4 text-sm text-muted-foreground">
          <Loader2 className="size-4 animate-spin" />
          Carregando status…
        </div>
      ) : error ? (
        <p className="rounded-md border border-dashed p-4 text-center text-sm text-destructive">
          Não foi possível carregar o quadro de status.
        </p>
      ) : rows.length === 0 ? (
        <p className="rounded-md border border-dashed p-4 text-center text-sm text-muted-foreground">
          Sem análises planejadas ainda. Defina a matriz para acompanhar o status.
        </p>
      ) : (
        <div className="overflow-hidden rounded-lg border">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b bg-muted/30 text-left text-xs uppercase tracking-wide text-muted-foreground">
                <th className="px-4 py-2.5 font-medium">Amostra</th>
                <th className="px-4 py-2.5 font-medium">Análise</th>
                <th className="px-4 py-2.5 font-medium">Responsável</th>
                <th className="px-4 py-2.5 font-medium">Amostras</th>
                <th className="px-4 py-2.5 font-medium">Pendentes</th>
                <th className="px-4 py-2.5 font-medium">Concluídas</th>
                <th className="px-4 py-2.5 font-medium">Status</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((row) => (
                <tr
                  key={`${row.sampleType}:${row.analysisName}`}
                  className="border-b last:border-0"
                >
                  <td className="px-4 py-3 font-medium">
                    {sampleTypeLabel[row.sampleType as SampleType] ?? row.sampleType}
                  </td>
                  <td className="px-4 py-3">{row.analysisName}</td>
                  <td className="px-4 py-3 text-muted-foreground">
                    {row.responsibleUserId
                      ? memberNameById.get(row.responsibleUserId) ?? '—'
                      : '—'}
                  </td>
                  <td className="px-4 py-3 text-muted-foreground">{row.collectedSamples}</td>
                  <td
                    className={cn(
                      'px-4 py-3',
                      row.pendingAnalyses > 0
                        ? 'font-medium text-amber-600'
                        : 'text-muted-foreground',
                    )}
                  >
                    {row.pendingAnalyses}
                  </td>
                  <td className="px-4 py-3 text-muted-foreground">{row.completedAnalyses}</td>
                  <td className="px-4 py-3">
                    {row.isDone ? (
                      <Badge variant="default">
                        <CheckCircle2 className="size-3.5" />
                        Concluído
                      </Badge>
                    ) : (
                      <Badge variant="secondary">Pendente</Badge>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}
