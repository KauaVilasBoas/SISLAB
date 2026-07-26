import { useMemo } from 'react';
import { CheckCircle2, Loader2, ShieldCheck } from 'lucide-react';
import { Button } from '@/shared/components/ui/button';
import { Badge } from '@/shared/components/ui/badge';
import { useToast } from '@/shared/components/ui/toast';
import type { ApiError } from '@/shared/types/api';
import { RequirePermission } from '@/modules/auth/PermissionsProvider';
import { Permissions } from '@/modules/auth/permissions';
import {
  useExperimentalModel,
  useInclusionCriteria,
} from '@/modules/configuration/api/configuration.queries';
import { useApplySelection, useSelection } from '@/modules/in-vivo/api/projects.queries';
import {
  animalSexLabel,
  formatMeasurement,
  inclusionStatusPresentation,
} from '@/modules/in-vivo/presentation';
import type {
  AnimalSelectionListItem,
  BatchDetail,
  InclusionStatus,
} from '@/modules/in-vivo/types';

/**
 * Animal selection panel for one batch (SISLAB-02): the "Aplicar seleção" action (POST apply-selection) plus the
 * board (GET selection) listing each animal as included/excluded with the deciding value and reason. Applying is
 * gated by `Projects.ApplySelection`.
 *
 * Non-applicable is surfaced, not blocked: the batch's bound model (SISLAB-04) decides which parameters apply, so
 * a configured criterion on a parameter the model does not list is ignored by the backend. This panel cross-checks
 * the company's inclusion criteria against the model's parameters and, when at least one criterion falls outside
 * the model, shows a "não aplicável" note — mirroring the backend behaviour without treating it as an error.
 */
export function AnimalSelectionPanel({
  projectId,
  batch,
}: {
  projectId: string;
  batch: BatchDetail;
}) {
  const toast = useToast();
  const applySelection = useApplySelection(projectId, batch.id);
  const selection = useSelection(projectId, batch.id);
  const criteria = useInclusionCriteria();
  const model = useExperimentalModel(batch.experimentalModelId);

  // Criteria whose parameter the batch's model does not list — backend ignores them (non-blocking).
  const nonApplicable = useMemo(() => {
    const criteriaList = criteria.data;
    if (!criteriaList || criteriaList.length === 0) return [];
    const applicable = new Set(
      (model.data?.parameters ?? []).map((code) => code.toLowerCase()),
    );
    // With no model bound, no parameter is applicable, so every criterion is non-applicable.
    return criteriaList.filter((c) => !applicable.has(c.parameterCode.toLowerCase()));
  }, [criteria.data, model.data]);

  async function handleApply() {
    try {
      const decided = await applySelection.mutateAsync();
      toast(
        'success',
        decided > 0
          ? `Seleção aplicada — ${decided} animal(is) avaliado(s).`
          : 'Seleção aplicada, mas nenhum animal foi avaliado (verifique modelo, critérios e leituras).',
      );
    } catch (err) {
      toast('error', (err as ApiError)?.message ?? 'Não foi possível aplicar a seleção.');
    }
  }

  const rows = selection.data ?? [];
  const includedCount = rows.filter((row) => row.inclusionStatus === 'Included').length;
  const excludedCount = rows.filter((row) => row.inclusionStatus === 'Excluded').length;

  return (
    <div className="border-t p-4">
      <div className="mb-3 flex flex-wrap items-center justify-between gap-3">
        <div className="flex items-center gap-2">
          <ShieldCheck className="size-4 text-muted-foreground" />
          <span className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
            Seleção de animais
          </span>
          {rows.length > 0 && (
            <span className="text-xs text-muted-foreground">
              {includedCount} incluído(s) · {excludedCount} excluído(s)
            </span>
          )}
        </div>
        <RequirePermission code={Permissions.projects.applySelection}>
          <Button
            variant="outline"
            size="sm"
            onClick={handleApply}
            disabled={applySelection.isPending}
          >
            {applySelection.isPending ? (
              <Loader2 className="size-4 animate-spin" />
            ) : (
              <CheckCircle2 className="size-4" />
            )}
            Aplicar seleção
          </Button>
        </RequirePermission>
      </div>

      {nonApplicable.length > 0 && (
        <p className="mb-3 rounded-md border border-dashed bg-muted/30 p-3 text-xs text-muted-foreground">
          {nonApplicable.length === 1
            ? 'O critério a seguir não se aplica ao modelo da leva e será ignorado: '
            : 'Os critérios a seguir não se aplicam ao modelo da leva e serão ignorados: '}
          <span className="font-medium">
            {nonApplicable.map((c) => c.parameterCode).join(', ')}
          </span>
          .
        </p>
      )}

      {selection.isLoading ? (
        <div className="flex items-center justify-center gap-2 py-4 text-sm text-muted-foreground">
          <Loader2 className="size-4 animate-spin" />
          Carregando seleção…
        </div>
      ) : selection.isError ? (
        <p className="py-4 text-center text-sm text-destructive">
          Não foi possível carregar a seleção.
        </p>
      ) : rows.length === 0 ? (
        <p className="rounded-md border border-dashed p-4 text-center text-sm text-muted-foreground">
          Nenhum animal na leva.
        </p>
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="text-xs text-muted-foreground">
                <th className="pb-2 pr-3 text-left font-medium">Animal</th>
                <th className="pb-2 pr-3 text-left font-medium">Caixa</th>
                <th className="pb-2 pr-3 text-left font-medium">Grupo</th>
                <th className="pb-2 pr-3 text-left font-medium">Decisão</th>
                <th className="pb-2 pr-3 text-right font-medium">Valor decisivo</th>
                <th className="pb-2 text-left font-medium">Motivo</th>
              </tr>
            </thead>
            <tbody className="divide-y">
              {rows.map((row) => (
                <SelectionRow key={row.id} row={row} />
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

function SelectionRow({ row }: { row: AnimalSelectionListItem }) {
  const presentation =
    row.inclusionStatus != null
      ? inclusionStatusPresentation[row.inclusionStatus as InclusionStatus]
      : null;

  return (
    <tr>
      <td className="py-2 pr-3">
        <span className="font-medium">{row.identifier}</span>
        <span className="ml-2 text-xs text-muted-foreground">
          {animalSexLabel[row.sex as keyof typeof animalSexLabel] ?? row.sex}
        </span>
      </td>
      <td className="py-2 pr-3 text-muted-foreground">{row.cageName}</td>
      <td className="py-2 pr-3 text-muted-foreground">{row.groupName ?? '—'}</td>
      <td className="py-2 pr-3">
        {presentation ? (
          <Badge variant={presentation.variant}>{presentation.label}</Badge>
        ) : (
          <Badge variant="secondary">Pendente</Badge>
        )}
      </td>
      <td className="py-2 pr-3 text-right tabular-nums">
        {row.inclusionDecidingValue != null
          ? formatMeasurement(row.inclusionDecidingValue, null)
          : '—'}
        {row.inclusionParameterCode ? (
          <span className="ml-1 text-xs text-muted-foreground">
            {row.inclusionParameterCode}
          </span>
        ) : null}
      </td>
      <td className="py-2 text-xs text-muted-foreground">{row.inclusionReason ?? '—'}</td>
    </tr>
  );
}
