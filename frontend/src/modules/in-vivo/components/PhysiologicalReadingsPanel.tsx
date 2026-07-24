import { useMemo, useState, type FormEvent } from 'react';
import { Activity, Loader2, Plus } from 'lucide-react';
import { Button } from '@/shared/components/ui/button';
import { Modal } from '@/shared/components/ui/modal';
import { Input } from '@/shared/components/ui/input';
import { Label } from '@/shared/components/ui/label';
import { SingleSelect } from '@/shared/components/ui/single-select';
import { useToast } from '@/shared/components/ui/toast';
import type { ApiError } from '@/shared/types/api';
import { RequirePermission } from '@/modules/auth/PermissionsProvider';
import { Permissions } from '@/modules/auth/permissions';
import { useExperimentalModel } from '@/modules/configuration/api/configuration.queries';
import { useReadings, useRecordReading } from '@/modules/in-vivo/api/projects.queries';
import { formatDate, formatMeasurement } from '@/modules/in-vivo/presentation';
import type { AnimalDetail, BatchDetail } from '@/modules/in-vivo/types';

/** Human label for each applicable-parameter code (SISLAB-04), mirroring BatchModelPanel; falls back to the code. */
const parameterLabel: Record<string, string> = {
  glicemia: 'Glicemia',
  rotarod: 'Rotarod',
  peso: 'Peso',
};

/** Default unit suggestion per known parameter — only a convenience prefill; the operator may override. */
const defaultUnitByParameter: Record<string, string> = {
  glicemia: 'mg/dL',
  peso: 'g',
  rotarod: 's',
};

/** Flattens a batch's cages into a single animal list, preserving the cage order. */
function batchAnimals(batch: BatchDetail): { animal: AnimalDetail; cageName: string }[] {
  return batch.cages.flatMap((cage) =>
    cage.animals.map((animal) => ({ animal, cageName: cage.name })),
  );
}

/**
 * Physiological readings panel for one batch (SISLAB-02): register glicemia/peso (extensible) per animal per
 * timepoint, and list what was recorded. The offered timepoints/parameters are filtered by the batch's bound
 * experimental model (SISLAB-04 #3) — only `model.timepoints` and only the parameters whose code is in
 * `model.parameters` are offered (e.g. no glicemia field when the model does not list it). When the batch has no
 * model bound the panel falls back to free-text parameter/timepoint (default behaviour) instead of blocking.
 */
export function PhysiologicalReadingsPanel({
  projectId,
  batch,
}: {
  projectId: string;
  batch: BatchDetail;
}) {
  const [recording, setRecording] = useState(false);
  const model = useExperimentalModel(batch.experimentalModelId);
  const readings = useReadings(projectId);

  const animals = useMemo(() => batchAnimals(batch), [batch]);
  const batchAnimalIds = useMemo(
    () => new Set(animals.map((entry) => entry.animal.id)),
    [animals],
  );
  // The readings endpoint is project-scoped; narrow to this batch's animals so the panel shows only its rows.
  const batchReadings = (readings.data ?? []).filter((reading) =>
    batchAnimalIds.has(reading.animalId),
  );

  const hasModel = Boolean(batch.experimentalModelId);
  const modelReady = hasModel && model.data != null;

  return (
    <div className="border-t p-4">
      <div className="mb-3 flex flex-wrap items-center justify-between gap-3">
        <div className="flex items-center gap-2">
          <Activity className="size-4 text-muted-foreground" />
          <span className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
            Leituras fisiológicas
          </span>
        </div>
        <RequirePermission code={Permissions.projects.recordReading}>
          <Button
            variant="outline"
            size="sm"
            onClick={() => setRecording(true)}
            disabled={animals.length === 0}
          >
            <Plus className="size-4" />
            Registrar leitura
          </Button>
        </RequirePermission>
      </div>

      {hasModel && model.isLoading ? (
        <p className="py-3 text-center text-xs text-muted-foreground">Carregando modelo…</p>
      ) : null}

      {animals.length === 0 ? (
        <p className="rounded-md border border-dashed p-4 text-center text-sm text-muted-foreground">
          Nenhum animal alojado. Aloje animais nas caixas antes de registrar leituras.
        </p>
      ) : readings.isLoading ? (
        <div className="flex items-center justify-center gap-2 py-4 text-sm text-muted-foreground">
          <Loader2 className="size-4 animate-spin" />
          Carregando leituras…
        </div>
      ) : readings.isError ? (
        <p className="py-4 text-center text-sm text-destructive">
          Não foi possível carregar as leituras.
        </p>
      ) : batchReadings.length === 0 ? (
        <p className="rounded-md border border-dashed p-4 text-center text-sm text-muted-foreground">
          Nenhuma leitura registrada nesta leva.
        </p>
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="text-xs text-muted-foreground">
                <th className="pb-2 pr-3 text-left font-medium">Animal</th>
                <th className="pb-2 pr-3 text-left font-medium">Parâmetro</th>
                <th className="pb-2 pr-3 text-left font-medium">Timepoint</th>
                <th className="pb-2 pr-3 text-right font-medium">Valor</th>
                <th className="pb-2 text-right font-medium">Registrado</th>
              </tr>
            </thead>
            <tbody className="divide-y">
              {batchReadings.map((reading) => (
                <tr key={reading.id}>
                  <td className="py-2 pr-3 font-medium">{reading.animalIdentifier}</td>
                  <td className="py-2 pr-3 text-muted-foreground">
                    {parameterLabel[reading.parameterCode] ?? reading.parameterCode}
                  </td>
                  <td className="py-2 pr-3 text-muted-foreground">{reading.timepointLabel}</td>
                  <td className="py-2 pr-3 text-right tabular-nums">
                    {formatMeasurement(reading.value, reading.unit)}
                  </td>
                  <td className="py-2 text-right text-xs text-muted-foreground">
                    {formatDate(reading.recordedAtUtc)}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {recording && (
        <RecordReadingModal
          projectId={projectId}
          animals={animals}
          modelTimepoints={modelReady ? model.data!.timepoints : null}
          modelParameters={modelReady ? model.data!.parameters : null}
          onClose={() => setRecording(false)}
        />
      )}
    </div>
  );
}

interface RecordReadingModalProps {
  projectId: string;
  animals: { animal: AnimalDetail; cageName: string }[];
  /** The batch model's timepoints (SISLAB-04 #3), or null to fall back to free-text entry (no model bound). */
  modelTimepoints: string[] | null;
  /** The batch model's applicable parameter codes, or null to fall back to free-text entry (no model bound). */
  modelParameters: string[] | null;
  onClose: () => void;
}

function RecordReadingModal({
  projectId,
  animals,
  modelTimepoints,
  modelParameters,
  onClose,
}: RecordReadingModalProps) {
  const record = useRecordReading(projectId);
  const toast = useToast();

  const hasParameterOptions = modelParameters != null && modelParameters.length > 0;
  const hasTimepointOptions = modelTimepoints != null && modelTimepoints.length > 0;

  const [animalId, setAnimalId] = useState<string | null>(animals[0]?.animal.id ?? null);
  const [parameterCode, setParameterCode] = useState<string>(
    hasParameterOptions ? modelParameters![0] : '',
  );
  const [timepointLabel, setTimepointLabel] = useState<string>(
    hasTimepointOptions ? modelTimepoints![0] : '',
  );
  const [value, setValue] = useState('');
  const [unit, setUnit] = useState<string>(
    hasParameterOptions ? (defaultUnitByParameter[modelParameters![0]] ?? '') : '',
  );

  const animalOptions = animals.map(({ animal, cageName }) => ({
    value: animal.id,
    label: animal.identifier,
    hint: cageName,
  }));

  const parameterOptions = hasParameterOptions
    ? modelParameters!.map((code) => ({
        value: code,
        label: parameterLabel[code] ?? code,
      }))
    : [];

  const timepointOptions = hasTimepointOptions
    ? modelTimepoints!.map((label) => ({ value: label, label }))
    : [];

  /** Prefills the unit suggestion when picking a known parameter, without clobbering a manual edit. */
  function pickParameter(code: string | null) {
    if (code == null) return;
    setParameterCode(code);
    const suggestedUnit = defaultUnitByParameter[code];
    if (suggestedUnit && unit.trim().length === 0) setUnit(suggestedUnit);
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!animalId) {
      toast('error', 'Escolha o animal.');
      return;
    }
    const code = parameterCode.trim();
    const timepoint = timepointLabel.trim();
    if (code.length === 0) {
      toast('error', 'Informe o parâmetro.');
      return;
    }
    if (timepoint.length === 0) {
      toast('error', 'Informe o timepoint.');
      return;
    }
    const parsedValue = Number(value.trim());
    if (!Number.isFinite(parsedValue)) {
      toast('error', 'Informe um valor numérico válido.');
      return;
    }
    if (unit.trim().length === 0) {
      toast('error', 'Informe a unidade.');
      return;
    }

    try {
      await record.mutateAsync({
        animalId,
        body: { parameterCode: code, value: parsedValue, unit: unit.trim(), timepointLabel: timepoint },
      });
      toast('success', 'Leitura registrada com sucesso.');
      onClose();
    } catch (err) {
      toast('error', (err as ApiError)?.message ?? 'Não foi possível registrar a leitura.');
    }
  }

  return (
    <Modal
      open
      onClose={onClose}
      title="Registrar leitura fisiológica"
      description={
        modelParameters == null
          ? 'A leva não tem modelo vinculado — informe o parâmetro e o timepoint livremente.'
          : 'Parâmetros e timepoints são os do modelo experimental vinculado à leva.'
      }
      footer={
        <>
          <Button variant="outline" onClick={onClose} disabled={record.isPending}>
            Cancelar
          </Button>
          <Button type="submit" form="record-reading-form" disabled={record.isPending}>
            {record.isPending && <Loader2 className="size-4 animate-spin" />}
            Registrar
          </Button>
        </>
      }
    >
      <form id="record-reading-form" className="flex flex-col gap-4" onSubmit={handleSubmit} noValidate>
        <div className="flex flex-col gap-2">
          <Label>Animal</Label>
          <SingleSelect
            label="Animal"
            placeholder="Selecionar animal…"
            options={animalOptions}
            value={animalId}
            onChange={setAnimalId}
          />
        </div>

        <div className="flex flex-col gap-2">
          <Label htmlFor="reading-parameter">Parâmetro</Label>
          {hasParameterOptions ? (
            <SingleSelect
              label="Parâmetro"
              placeholder="Selecionar parâmetro…"
              options={parameterOptions}
              value={parameterCode || null}
              onChange={pickParameter}
            />
          ) : (
            <Input
              id="reading-parameter"
              placeholder="glicemia"
              value={parameterCode}
              onChange={(e) => setParameterCode(e.target.value)}
              required
            />
          )}
        </div>

        <div className="flex flex-col gap-2">
          <Label htmlFor="reading-timepoint">Timepoint</Label>
          {hasTimepointOptions ? (
            <SingleSelect
              label="Timepoint"
              placeholder="Selecionar timepoint…"
              options={timepointOptions}
              value={timepointLabel || null}
              onChange={(next) => next != null && setTimepointLabel(next)}
            />
          ) : (
            <Input
              id="reading-timepoint"
              placeholder="basal"
              value={timepointLabel}
              onChange={(e) => setTimepointLabel(e.target.value)}
              required
            />
          )}
        </div>

        <div className="grid grid-cols-2 gap-4">
          <div className="flex flex-col gap-2">
            <Label htmlFor="reading-value">Valor</Label>
            <Input
              id="reading-value"
              type="number"
              step="any"
              inputMode="decimal"
              placeholder="250"
              value={value}
              onChange={(e) => setValue(e.target.value)}
              required
            />
          </div>
          <div className="flex flex-col gap-2">
            <Label htmlFor="reading-unit">Unidade</Label>
            <Input
              id="reading-unit"
              placeholder="mg/dL"
              value={unit}
              onChange={(e) => setUnit(e.target.value)}
              required
            />
          </div>
        </div>
      </form>
    </Modal>
  );
}
