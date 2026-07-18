import { useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { ArrowLeft, Loader2, Plus, ClipboardCheck } from 'lucide-react';
import { PageHeader } from '@/shared/components/PageHeader';
import { Button } from '@/shared/components/ui/button';
import { Badge } from '@/shared/components/ui/badge';
import { RequirePermission } from '@/modules/auth/PermissionsProvider';
import { Permissions } from '@/modules/auth/permissions';
import { useSample } from '@/modules/in-vivo/api/biobank.queries';
import {
  AnalyseSampleModal,
  RecordResultModal,
} from '@/modules/in-vivo/components/SampleAnalysisModals';
import {
  analysisStatusPresentation,
  formatAmount,
  formatDate,
  sampleTypeName,
} from '@/modules/in-vivo/presentation';
import type { AnalysisStatus } from '@/modules/in-vivo/types';

/**
 * Biobank sample detail (card [E11] #89): the header (origin, conservation, collection), the derived remaining
 * balance and the analyses run against it, with permission-gated actions to run a new analysis (consuming an
 * aliquot) and record a pending analysis' result. A thin shell around the detail query and its modals.
 */
export function SampleDetailPage() {
  const { sampleId = '' } = useParams();
  const navigate = useNavigate();

  const { data: sample, isLoading, isError } = useSample(sampleId);

  const [analysing, setAnalysing] = useState(false);
  const [recordingFor, setRecordingFor] = useState<{ id: string; name: string } | null>(
    null,
  );

  if (isLoading) {
    return (
      <div className="flex items-center justify-center gap-2 p-16 text-sm text-muted-foreground">
        <Loader2 className="size-4 animate-spin" />
        Carregando amostra…
      </div>
    );
  }

  if (isError || !sample) {
    return (
      <div className="space-y-4">
        <Button
          variant="ghost"
          size="sm"
          onClick={() => navigate('/experiments/in-vivo/biobank')}
        >
          <ArrowLeft className="size-4" />
          Biobanco
        </Button>
        <p className="p-10 text-center text-sm text-destructive">
          Não foi possível carregar a amostra.
        </p>
      </div>
    );
  }

  const depleted = sample.remainingQuantity <= 0;
  const conservation =
    sample.conservationTempMinCelsius != null && sample.conservationTempMaxCelsius != null
      ? `${sample.conservationTempMinCelsius} °C a ${sample.conservationTempMaxCelsius} °C`
      : '—';

  return (
    <div className="space-y-6">
      <Button
        variant="ghost"
        size="sm"
        onClick={() => navigate('/experiments/in-vivo/biobank')}
      >
        <ArrowLeft className="size-4" />
        Biobanco
      </Button>

      <PageHeader
        title={sample.code}
        description={`${sampleTypeName(sample.type)} · coletado por ${sample.collectedBy} em ${formatDate(
          sample.collectedAtUtc,
        )}`}
        actions={
          <RequirePermission code={Permissions.samples.analyse}>
            <Button onClick={() => setAnalysing(true)} disabled={depleted}>
              <Plus className="size-4" />
              Nova análise
            </Button>
          </RequirePermission>
        }
      />

      <div className="grid gap-4 sm:grid-cols-3">
        <div className="rounded-lg border bg-card p-4">
          <p className="text-xs uppercase tracking-wide text-muted-foreground">
            Coletado
          </p>
          <p className="mt-1 text-lg font-semibold">
            {formatAmount(sample.collectedQuantity, sample.unit)}
          </p>
        </div>
        <div className="rounded-lg border bg-card p-4">
          <p className="text-xs uppercase tracking-wide text-muted-foreground">
            Consumido
          </p>
          <p className="mt-1 text-lg font-semibold">
            {formatAmount(sample.consumedQuantity, sample.unit)}
          </p>
        </div>
        <div className="rounded-lg border bg-card p-4">
          <p className="text-xs uppercase tracking-wide text-muted-foreground">Saldo</p>
          <p
            className={`mt-1 text-lg font-semibold ${depleted ? 'text-muted-foreground' : ''}`}
          >
            {formatAmount(sample.remainingQuantity, sample.unit)}
          </p>
        </div>
      </div>

      <div className="grid gap-4 sm:grid-cols-2">
        <div className="rounded-lg border bg-card p-4 text-sm">
          <p className="text-muted-foreground">Conservação</p>
          <p className="font-medium">{conservation}</p>
        </div>
        <div className="rounded-lg border bg-card p-4 text-sm">
          <p className="text-muted-foreground">Armazenamento</p>
          <p className="font-medium">{sample.storageLabel ?? '—'}</p>
        </div>
      </div>

      {sample.notes && (
        <p className="rounded-lg border bg-muted/40 p-4 text-sm text-muted-foreground">
          {sample.notes}
        </p>
      )}

      <div className="rounded-lg border bg-card">
        <div className="border-b p-4 text-sm font-semibold">Análises</div>
        {sample.analyses.length === 0 ? (
          <p className="p-6 text-center text-sm text-muted-foreground">
            Nenhuma análise ainda. Registre a primeira com “Nova análise”.
          </p>
        ) : (
          <table className="w-full text-sm">
            <thead className="border-b text-left text-xs uppercase tracking-wide text-muted-foreground">
              <tr>
                <th className="px-4 py-3 font-medium">Análise</th>
                <th className="px-4 py-3 font-medium">Consumido</th>
                <th className="px-4 py-3 font-medium">Status</th>
                <th className="px-4 py-3 font-medium">Resultado</th>
                <th className="px-4 py-3 font-medium">Por</th>
                <th className="px-4 py-3" />
              </tr>
            </thead>
            <tbody>
              {sample.analyses.map((analysis) => {
                const presentation =
                  analysisStatusPresentation[analysis.status as AnalysisStatus];
                return (
                  <tr key={analysis.id} className="border-b last:border-0">
                    <td className="px-4 py-3 font-medium">{analysis.name}</td>
                    <td className="px-4 py-3 text-muted-foreground">
                      {formatAmount(analysis.consumedQuantity, analysis.unit)}
                    </td>
                    <td className="px-4 py-3">
                      <Badge variant={presentation?.variant ?? 'muted'}>
                        {presentation?.label ?? analysis.status}
                      </Badge>
                    </td>
                    <td className="px-4 py-3 text-muted-foreground">
                      {analysis.result ?? '—'}
                    </td>
                    <td className="px-4 py-3 text-muted-foreground">
                      {analysis.performedBy}
                    </td>
                    <td className="px-4 py-3 text-right">
                      {analysis.status === 'Pending' && (
                        <RequirePermission code={Permissions.samples.recordResult}>
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={() =>
                              setRecordingFor({ id: analysis.id, name: analysis.name })
                            }
                          >
                            <ClipboardCheck className="size-4" />
                            Resultado
                          </Button>
                        </RequirePermission>
                      )}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        )}
      </div>

      {analysing && (
        <AnalyseSampleModal
          sampleId={sample.id}
          unit={sample.unit}
          remaining={sample.remainingQuantity}
          onClose={() => setAnalysing(false)}
        />
      )}
      {recordingFor && (
        <RecordResultModal
          sampleId={sample.id}
          analysisId={recordingFor.id}
          analysisName={recordingFor.name}
          onClose={() => setRecordingFor(null)}
        />
      )}
    </div>
  );
}
