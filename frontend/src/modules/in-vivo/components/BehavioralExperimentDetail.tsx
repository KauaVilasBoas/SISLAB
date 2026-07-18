import { ArrowLeft, Calculator, CheckCircle2, Download, Loader2 } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { PageHeader } from '@/shared/components/PageHeader';
import { Button } from '@/shared/components/ui/button';
import { Badge } from '@/shared/components/ui/badge';
import { useToast } from '@/shared/components/ui/toast';
import { RequirePermission } from '@/modules/auth/PermissionsProvider';
import { Permissions } from '@/modules/auth/permissions';
import type { ApiError } from '@/shared/types/api';
import {
  experimentStatusPresentation,
  formatDate,
} from '@/modules/experiments/components/experiment-presentation';
import type { ExperimentDetail, ExperimentStatus } from '@/modules/experiments/types';
import { behavioralTypePresentation } from '@/modules/in-vivo/presentation';
import type { BehavioralType } from '@/modules/in-vivo/types';
import {
  useCalculateBehavioralExperiment,
  useExportBehavioralExperiment,
} from '@/modules/in-vivo/api/behavioral.queries';

/**
 * Detail view for an in vivo behavioural experiment (card [E11] #88). Rendered by the shared
 * ExperimentDetailPage when the experiment type is behavioural (von Frey / tail-flick / rota-rod / hemogram).
 * Shows the timepoint step flow and drives the two type-specific actions: run the versioned calculation and
 * export the group × timepoint Prism CSV (card #31). The per-timepoint reading launch is recorded from the
 * project's animals; it opens once the behavioural detail exposes the batch's animals (see the module TODO).
 */
export function BehavioralExperimentDetail({
  experiment,
}: {
  experiment: ExperimentDetail;
}) {
  const navigate = useNavigate();
  const toast = useToast();

  const calculate = useCalculateBehavioralExperiment(experiment.id);
  const exportInVivo = useExportBehavioralExperiment(experiment.id);

  const presentation =
    experimentStatusPresentation[experiment.status as ExperimentStatus];
  const type = behavioralTypePresentation[experiment.type as BehavioralType];
  const isCalculated = experiment.calculation != null;
  const hasRecordedTimepoint = experiment.steps.some(
    (step) => step.kind === 'Timepoint' && step.performedAtUtc != null,
  );
  const canExport =
    isCalculated &&
    (experiment.status === 'AwaitingAnalysis' || experiment.status === 'Completed');

  async function handleCalculate() {
    try {
      await calculate.mutateAsync();
      toast('success', 'Cálculo aplicado.');
    } catch (err) {
      toast('error', (err as ApiError)?.message ?? 'Não foi possível calcular o teste.');
    }
  }

  async function handleExport() {
    try {
      await exportInVivo.mutateAsync();
      toast('success', 'Exportação gerada.');
    } catch (err) {
      toast('error', (err as ApiError)?.message ?? 'Não foi possível exportar o teste.');
    }
  }

  return (
    <div className="space-y-6">
      <Button variant="ghost" size="sm" onClick={() => navigate('/experiments')}>
        <ArrowLeft className="size-4" />
        Experimentos
      </Button>

      <PageHeader
        title={experiment.title}
        description={
          experiment.description ?? type?.description ?? 'Teste comportamental in vivo'
        }
        actions={
          <div className="flex items-center gap-2">
            <Badge variant={presentation?.variant ?? 'muted'}>
              {presentation?.label ?? experiment.status}
            </Badge>
            {type?.scorable && (
              <RequirePermission code={Permissions.experiments.calculateBehavioral}>
                <Button
                  size="sm"
                  onClick={() => void handleCalculate()}
                  disabled={!hasRecordedTimepoint || isCalculated || calculate.isPending}
                >
                  {calculate.isPending ? (
                    <Loader2 className="size-4 animate-spin" />
                  ) : (
                    <Calculator className="size-4" />
                  )}
                  Calcular
                </Button>
              </RequirePermission>
            )}
            <Button
              variant="outline"
              size="sm"
              onClick={() => void handleExport()}
              disabled={!canExport || exportInVivo.isPending}
            >
              {exportInVivo.isPending ? (
                <Loader2 className="size-4 animate-spin" />
              ) : (
                <Download className="size-4" />
              )}
              Exportar para Prism
            </Button>
          </div>
        }
      />

      <div className="rounded-lg border bg-card p-5">
        <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-muted-foreground">
          Fluxo de timepoints
        </h2>
        <ol className="space-y-3">
          {experiment.steps.map((step) => (
            <li key={step.order} className="flex items-start gap-3">
              <span
                className={
                  step.performedAtUtc
                    ? 'mt-0.5 flex size-5 shrink-0 items-center justify-center rounded-full bg-emerald-500 text-[10px] font-bold text-white'
                    : 'mt-0.5 flex size-5 shrink-0 items-center justify-center rounded-full border text-[10px] font-bold text-muted-foreground'
                }
              >
                {step.order + 1}
              </span>
              <div className="min-w-0">
                <p className="text-sm font-medium">{step.title}</p>
                <p className="text-xs text-muted-foreground">
                  {step.performedAtUtc
                    ? `${step.performedBy ?? '—'} · ${formatDate(step.performedAtUtc)}`
                    : 'Pendente'}
                </p>
              </div>
            </li>
          ))}
        </ol>
      </div>

      {isCalculated && (
        <div className="flex items-start gap-2 rounded-md border border-emerald-200 bg-emerald-50 p-3 text-sm text-emerald-900">
          <CheckCircle2 className="mt-0.5 size-4 shrink-0" />
          <div>
            <p className="font-medium">
              Cálculo aplicado ({experiment.calculation!.formulaName})
            </p>
            <p className="text-xs text-emerald-800">
              {experiment.calculation!.formulaExpression}
            </p>
          </div>
        </div>
      )}
    </div>
  );
}
