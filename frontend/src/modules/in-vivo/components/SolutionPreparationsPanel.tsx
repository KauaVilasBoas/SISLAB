import { Loader2, FlaskConical } from 'lucide-react';
import { Badge } from '@/shared/components/ui/badge';
import { usePreparations } from '@/modules/in-vivo/api/projects.queries';
import {
  compoundStateName,
  formatDate,
  formatMicrolitres,
} from '@/modules/in-vivo/presentation';
import type { SolutionPreparationListItem } from '@/modules/in-vivo/types';

/**
 * Confirmed solution preparations panel (SISLAB-01): lists the frozen dose × weight snapshots of the project with the
 * exact volumes the operator confirmed — compound mass, compound volume (Liquid), final volume and diluent — plus the
 * author and instant. It reproduces the numbers so the operator double-checks the µL against the bench. A read-only
 * companion to the {@link PrepareSolutionModal}; the modal invalidates this list so a new preparation appears at once.
 */
export function SolutionPreparationsPanel({ projectId }: { projectId: string }) {
  const { data, isLoading, isError } = usePreparations(projectId);

  return (
    <section className="space-y-3">
      <div className="flex items-center gap-2">
        <FlaskConical className="size-4 text-muted-foreground" />
        <h2 className="text-sm font-semibold uppercase tracking-wide text-muted-foreground">
          Preparos de solução
        </h2>
      </div>

      {isLoading ? (
        <div className="flex items-center justify-center gap-2 rounded-lg border bg-card p-8 text-sm text-muted-foreground">
          <Loader2 className="size-4 animate-spin" />
          Carregando preparos…
        </div>
      ) : isError ? (
        <p className="rounded-lg border bg-card p-8 text-center text-sm text-destructive">
          Não foi possível carregar os preparos.
        </p>
      ) : (data?.length ?? 0) === 0 ? (
        <p className="rounded-lg border border-dashed bg-card p-8 text-center text-sm text-muted-foreground">
          Nenhum preparo confirmado ainda. Use “Preparar solução” em um grupo.
        </p>
      ) : (
        <div className="grid gap-3 md:grid-cols-2">
          {data!.map((preparation) => (
            <PreparationCard key={preparation.id} preparation={preparation} />
          ))}
        </div>
      )}
    </section>
  );
}

function PreparationCard({ preparation }: { preparation: SolutionPreparationListItem }) {
  return (
    <article className="space-y-3 rounded-lg border bg-card p-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <span className="font-medium">{preparation.groupName}</span>
        {preparation.isVehicleOnly ? (
          <Badge variant="outline">Controle (só veículo)</Badge>
        ) : (
          <Badge variant="secondary">
            {Number(preparation.doseAmountGramsPerKilogram)} g/kg ·{' '}
            {compoundStateName(preparation.compoundState)}
          </Badge>
        )}
      </div>

      <dl className="space-y-1.5 text-sm">
        {!preparation.isVehicleOnly && (
          <Row label="Massa do composto" value={`${Number(preparation.compoundMassGrams)} g`} />
        )}
        {preparation.compoundVolumeMicrolitres != null && (
          <Row
            label="Volume do composto"
            value={formatMicrolitres(preparation.compoundVolumeMicrolitres)}
          />
        )}
        <Row
          label="Volume final"
          value={formatMicrolitres(preparation.finalVolumeMicrolitres)}
          strong
        />
        <Row
          label="Diluente"
          value={formatMicrolitres(preparation.diluentVolumeMicrolitres)}
          strong
        />
      </dl>

      <div className="flex flex-wrap items-center justify-between gap-2 border-t pt-2 text-xs text-muted-foreground">
        <span>
          1 g : {Number(preparation.relationMicrolitresPerGram)} µL ·{' '}
          {Number(preparation.relationWeightGrams)} g
        </span>
        <span>
          {preparation.preparedBy} · {formatDate(preparation.preparedAtUtc)}
        </span>
      </div>
    </article>
  );
}

function Row({
  label,
  value,
  strong = false,
}: {
  label: string;
  value: string;
  strong?: boolean;
}) {
  return (
    <div className="flex items-center justify-between gap-2">
      <dt className="text-muted-foreground">{label}</dt>
      <dd className={strong ? 'font-semibold tabular-nums' : 'tabular-nums'}>{value}</dd>
    </div>
  );
}
