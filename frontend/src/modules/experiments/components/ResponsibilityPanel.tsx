import { useMemo } from 'react';
import { Loader2, UserCog, X } from 'lucide-react';
import { Badge } from '@/shared/components/ui/badge';
import { SingleSelect } from '@/shared/components/ui/single-select';
import { MultiSelect, type MultiSelectOption } from '@/shared/components/ui/multi-select';
import { useToast } from '@/shared/components/ui/toast';
import { useHasPermission } from '@/modules/auth/PermissionsProvider';
import { Permissions } from '@/modules/auth/permissions';
import { useMembers } from '@/modules/identity/api/identity.queries';
import type { ApiError } from '@/shared/types/api';
import type { EnrichedMemberDto } from '@/modules/identity/types';
import type { ExperimentDetail, ExperimentStepDetail } from '@/modules/experiments/types';
import {
  useAssignResponsible,
  useAssignStepResponsible,
  useRemoveStepResponsible,
} from '@/modules/experiments/api/experiments.queries';

/** Turns a member into a "Name" (or e-mail fallback) for display, and an option for the selects. */
function memberLabel(member: EnrichedMemberDto): string {
  return member.username || member.email;
}

/** Maps an ApiError to a friendly message, with a dedicated line for the ownership 403 the backend returns. */
function responsibilityError(err: unknown, fallback: string): string {
  const apiError = err as ApiError;
  if (apiError?.status === 403) {
    return 'Você não tem permissão para gerenciar responsáveis deste experimento.';
  }
  return apiError?.message ?? fallback;
}

/**
 * Responsibility management (card [E11]). Shows and edits the experiment's single lead responsible and the
 * one-or-many responsibles of each step, resolving every Lumen user id to a member name/e-mail via the enriched
 * members endpoint. Editing controls are gated by the same permission codes the backend's [RequirePermission]
 * enforces (defense-in-depth); a backend 403 (not a lead/step responsible) surfaces as a friendly toast.
 *
 * Reused by both the in vitro plate detail and the in vivo behavioural detail — steps are generic across types.
 */
export function ResponsibilityPanel({ experiment }: { experiment: ExperimentDetail }) {
  const toast = useToast();
  const { data: members, isLoading: membersLoading } = useMembers();

  const canAssignLead = useHasPermission(Permissions.experimentResponsibility.assignResponsible);
  const canAssignStep = useHasPermission(
    Permissions.experimentResponsibility.assignStepResponsible,
  );
  const canRemoveStep = useHasPermission(
    Permissions.experimentResponsibility.removeStepResponsible,
  );

  const assignLead = useAssignResponsible(experiment.id);
  const assignStep = useAssignStepResponsible(experiment.id);
  const removeStep = useRemoveStepResponsible(experiment.id);

  // userId -> display name, so step chips and the lead line read as names rather than raw guids.
  const nameById = useMemo(() => {
    const map = new Map<string, string>();
    (members ?? []).forEach((m) => map.set(m.userId, memberLabel(m)));
    return map;
  }, [members]);

  const memberOptions: MultiSelectOption[] = useMemo(
    () =>
      (members ?? []).map((m) => ({
        value: m.userId,
        label: memberLabel(m),
        hint: m.username ? m.email : undefined,
      })),
    [members],
  );

  const resolveName = (userId: string) => nameById.get(userId) ?? userId;

  async function handleAssignLead(userId: string | null) {
    if (!userId || userId === experiment.responsibleUserId) return;
    try {
      await assignLead.mutateAsync({ responsibleUserId: userId });
      toast('success', 'Responsável principal definido.');
    } catch (err) {
      toast('error', responsibilityError(err, 'Não foi possível definir o responsável.'));
    }
  }

  // Reconciles a step's responsibles to the selected set: adds the new ones, removes the dropped ones.
  async function handleStepChange(step: ExperimentStepDetail, next: string[]) {
    const current = new Set(step.responsibleUserIds);
    const nextSet = new Set(next);
    const toAdd = next.filter((id) => !current.has(id));
    const toRemove = step.responsibleUserIds.filter((id) => !nextSet.has(id));

    try {
      for (const userId of toAdd) {
        await assignStep.mutateAsync({ stepId: step.id, body: { responsibleUserId: userId } });
      }
      for (const userId of toRemove) {
        await removeStep.mutateAsync({ stepId: step.id, userId });
      }
      if (toAdd.length || toRemove.length) {
        toast('success', 'Responsáveis da etapa atualizados.');
      }
    } catch (err) {
      toast('error', responsibilityError(err, 'Não foi possível atualizar os responsáveis.'));
    }
  }

  async function handleRemoveStep(step: ExperimentStepDetail, userId: string) {
    try {
      await removeStep.mutateAsync({ stepId: step.id, userId });
      toast('success', 'Responsável removido da etapa.');
    } catch (err) {
      toast('error', responsibilityError(err, 'Não foi possível remover o responsável.'));
    }
  }

  const leadName = experiment.responsibleUserId
    ? resolveName(experiment.responsibleUserId)
    : null;
  const stepBusy = assignStep.isPending || removeStep.isPending;

  return (
    <div className="rounded-lg border bg-card p-5 text-sm">
      <div className="mb-3 flex items-center gap-2">
        <UserCog className="size-4 text-muted-foreground" aria-hidden="true" />
        <h2 className="text-sm font-semibold uppercase tracking-wide text-muted-foreground">
          Responsáveis
        </h2>
      </div>

      {/* Lead responsible */}
      <div className="space-y-1.5">
        <p className="text-xs font-medium text-muted-foreground">Responsável principal</p>
        {canAssignLead ? (
          <div className="flex items-center gap-2">
            <SingleSelect
              label="Responsável principal do experimento"
              placeholder="Definir responsável…"
              options={memberOptions}
              value={experiment.responsibleUserId}
              onChange={(value) => void handleAssignLead(value)}
              disabled={membersLoading || assignLead.isPending}
            />
            {assignLead.isPending && (
              <Loader2 className="size-4 animate-spin text-muted-foreground" aria-hidden="true" />
            )}
          </div>
        ) : (
          <p className="font-medium">{leadName ?? 'Sem responsável definido'}</p>
        )}
      </div>

      {/* Step responsibles */}
      <div className="mt-5 space-y-3">
        <p className="text-xs font-medium text-muted-foreground">Responsáveis por etapa</p>
        {experiment.steps.length === 0 ? (
          <p className="text-xs text-muted-foreground">Este experimento não tem etapas.</p>
        ) : (
          <ul className="space-y-3">
            {experiment.steps.map((step) => (
              <li key={step.id} className="rounded-md border p-3">
                <div className="flex items-center justify-between gap-2">
                  <p className="min-w-0 truncate text-sm font-medium">
                    {step.order + 1}. {step.title}
                  </p>
                  {canAssignStep && (
                    <MultiSelect
                      label={`Responsáveis da etapa ${step.title}`}
                      placeholder="Adicionar…"
                      className="h-8 w-40"
                      options={memberOptions}
                      selected={step.responsibleUserIds}
                      onChange={(values) => void handleStepChange(step, values)}
                      disabled={membersLoading || stepBusy}
                    />
                  )}
                </div>

                {step.responsibleUserIds.length > 0 ? (
                  <div className="mt-2 flex flex-wrap gap-1.5">
                    {step.responsibleUserIds.map((userId) => (
                      <Badge key={userId} variant="secondary" className="gap-1 pr-1">
                        {resolveName(userId)}
                        {canRemoveStep && (
                          <button
                            type="button"
                            onClick={() => void handleRemoveStep(step, userId)}
                            disabled={stepBusy}
                            aria-label={`Remover ${resolveName(userId)} da etapa`}
                            className="ml-0.5 rounded-full p-0.5 hover:bg-black/10 disabled:opacity-50"
                          >
                            <X className="size-3" aria-hidden="true" />
                          </button>
                        )}
                      </Badge>
                    ))}
                  </div>
                ) : (
                  <p className="mt-2 text-xs text-muted-foreground">Sem responsável designado.</p>
                )}
              </li>
            ))}
          </ul>
        )}
      </div>

      {!canAssignLead && !canAssignStep && (
        <p className="mt-4 text-xs text-muted-foreground">
          Você tem acesso somente à visualização dos responsáveis.
        </p>
      )}
    </div>
  );
}
