import { useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { ArrowLeft, Loader2, Upload, Calculator, Grid3x3, CheckCircle2, Download } from 'lucide-react';
import { PageHeader } from '@/shared/components/PageHeader';
import { Button } from '@/shared/components/ui/button';
import { Badge } from '@/shared/components/ui/badge';
import { useToast } from '@/shared/components/ui/toast';
import type { ApiError } from '@/shared/types/api';
import {
  useExperiment,
  usePlateResult,
  useCalculateExperiment,
  useExportExperiment,
} from '@/modules/experiments/api/experiments.queries';
import {
  experimentStatusPresentation,
  typePresentation,
  formatDate,
} from '@/modules/experiments/components/experiment-presentation';
import { PlateGrid } from '@/modules/experiments/components/PlateGrid';
import { DesignPlateModal } from '@/modules/experiments/components/DesignPlateModal';
import { ImportReadingModal } from '@/modules/experiments/components/ImportReadingModal';
import type { ExperimentStatus } from '@/modules/experiments/types';

/**
 * Experiment detail (card [E11] #68). Shows the header + status, the ordered step flow, the 8×12 plate
 * (tinted by role, with readings and — once calculated — % viability), and the actions that drive the
 * flow: design the plate, import the reader CSV, and run the versioned calculation. Reads the detail and
 * the plate-result grid; each action invalidates them so the page reflects the new state.
 */
export function ExperimentDetailPage() {
  const { id = '' } = useParams();
  const navigate = useNavigate();
  const toast = useToast();

  const { data: experiment, isLoading, isError } = useExperiment(id);
  const { data: plate } = usePlateResult(id);
  const calculate = useCalculateExperiment(id);
  const exportExperiment = useExportExperiment(id);

  const [designing, setDesigning] = useState(false);
  const [importing, setImporting] = useState(false);

  async function handleCalculate() {
    try {
      await calculate.mutateAsync();
      toast('success', 'Cálculo aplicado.');
    } catch (err) {
      toast('error', (err as ApiError)?.message ?? 'Não foi possível calcular o experimento.');
    }
  }

  async function handleExport() {
    try {
      await exportExperiment.mutateAsync();
      toast('success', 'Exportação gerada.');
    } catch (err) {
      toast('error', (err as ApiError)?.message ?? 'Não foi possível exportar o experimento.');
    }
  }

  if (isLoading) {
    return (
      <div className="flex items-center justify-center gap-2 p-10 text-sm text-muted-foreground">
        <Loader2 className="size-4 animate-spin" />
        Carregando experimento…
      </div>
    );
  }

  if (isError || !experiment) {
    return (
      <div className="space-y-4">
        <Button variant="ghost" size="sm" onClick={() => navigate('/experiments')}>
          <ArrowLeft className="size-4" />
          Voltar
        </Button>
        <p className="p-10 text-center text-sm text-destructive">
          Não foi possível carregar o experimento.
        </p>
      </div>
    );
  }

  const presentation = experimentStatusPresentation[experiment.status as ExperimentStatus];
  const type = typePresentation(experiment.type);
  const hasPlate = (plate?.wells.length ?? 0) > 0;
  const hasFullReading = hasPlate && plate!.wells.every((well) => well.rawAbsorbance != null);
  const isCalculated = experiment.calculation != null;
  const canExport =
    isCalculated &&
    (experiment.status === 'AwaitingAnalysis' || experiment.status === 'Completed');

  return (
    <div className="space-y-6">
      <Button variant="ghost" size="sm" onClick={() => navigate('/experiments')}>
        <ArrowLeft className="size-4" />
        Experimentos
      </Button>

      <PageHeader
        title={experiment.title}
        description={experiment.description ?? type.description}
        actions={
          <Badge variant={presentation?.variant ?? 'muted'}>
            {presentation?.label ?? experiment.status}
          </Badge>
        }
      />

      <div className="grid gap-6 lg:grid-cols-[1fr_320px]">
        {/* Plate + actions */}
        <div className="space-y-4 rounded-lg border bg-card p-5">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <h2 className="text-sm font-semibold uppercase tracking-wide text-muted-foreground">
              Placa 8×12
            </h2>
            <div className="flex flex-wrap gap-2">
              <Button variant="outline" size="sm" onClick={() => setDesigning(true)}>
                <Grid3x3 className="size-4" />
                Desenhar placa
              </Button>
              <Button
                variant="outline"
                size="sm"
                onClick={() => setImporting(true)}
                disabled={!hasPlate}
              >
                <Upload className="size-4" />
                Importar leitura
              </Button>
              <Button
                size="sm"
                onClick={() => void handleCalculate()}
                disabled={!hasFullReading || isCalculated || calculate.isPending}
              >
                {calculate.isPending ? (
                  <Loader2 className="size-4 animate-spin" />
                ) : (
                  <Calculator className="size-4" />
                )}
                Calcular
              </Button>
              <Button
                variant="outline"
                size="sm"
                onClick={() => void handleExport()}
                disabled={!canExport || exportExperiment.isPending}
              >
                {exportExperiment.isPending ? (
                  <Loader2 className="size-4 animate-spin" />
                ) : (
                  <Download className="size-4" />
                )}
                Exportar para Prism
              </Button>
            </div>
          </div>

          {hasPlate ? (
            <PlateGrid
              wells={plate!.wells}
              isCalculated={isCalculated}
              formatComputed={type.formatComputed}
            />
          ) : (
            <p className="rounded-md border border-dashed p-8 text-center text-sm text-muted-foreground">
              A placa ainda não foi desenhada. Use “Desenhar placa” para começar.
            </p>
          )}

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

        {/* Steps + metadata */}
        <aside className="space-y-4">
          <div className="rounded-lg border bg-card p-5">
            <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-muted-foreground">
              Etapas
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

          <div className="rounded-lg border bg-card p-5 text-sm">
            <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-muted-foreground">
              Detalhes
            </h2>
            <dl className="space-y-2">
              <div className="flex justify-between gap-2">
                <dt className="text-muted-foreground">Criado por</dt>
                <dd className="text-right font-medium">{experiment.createdBy}</dd>
              </div>
              <div className="flex justify-between gap-2">
                <dt className="text-muted-foreground">Criado em</dt>
                <dd className="text-right">{formatDate(experiment.createdAtUtc)}</dd>
              </div>
              <div className="flex justify-between gap-2">
                <dt className="text-muted-foreground">Tipo</dt>
                <dd className="text-right">{type.label}</dd>
              </div>
            </dl>
          </div>
        </aside>
      </div>

      {designing && (
        <DesignPlateModal
          experimentId={id}
          experimentType={experiment.type}
          onClose={() => setDesigning(false)}
        />
      )}
      {importing && (
        <ImportReadingModal experimentId={id} onClose={() => setImporting(false)} />
      )}
    </div>
  );
}
