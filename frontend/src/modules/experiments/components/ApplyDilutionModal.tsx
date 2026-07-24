import { useMemo, useState, type FormEvent } from 'react';
import { Loader2 } from 'lucide-react';
import { Modal } from '@/shared/components/ui/modal';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import { Label } from '@/shared/components/ui/label';
import { Switch } from '@/shared/components/ui/switch';
import { useToast } from '@/shared/components/ui/toast';
import type { ApiError } from '@/shared/types/api';
import type { DilutionSchemeParams } from '@/modules/experiments/types';
import { useApplyDilutionScheme, useDilutionScheme } from '@/modules/experiments/api/dilution.queries';

const microMolarFormatter = new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 4 });

/**
 * Apply-dilution-scheme dialog (SISLAB-05): populates a plate column's ConcentrationUm wells from a serial-dilution
 * scheme. It previews the series (the same stateless GET the calculator uses) so the operator confirms the numbers
 * before writing, then POSTs the recompute inputs plus the target column. The column length must match the number of
 * points (the backend enforces it); the preview makes the mismatch obvious. Permission-gated by the caller.
 */
export function ApplyDilutionModal({
  experimentId,
  onClose,
}: {
  experimentId: string;
  onClose: () => void;
}) {
  const toast = useToast();
  const apply = useApplyDilutionScheme(experimentId);

  const [column, setColumn] = useState('1');
  const [topConcentration, setTopConcentration] = useState('200');
  const [factor, setFactor] = useState('4');
  const [points, setPoints] = useState('6');
  const [finalVolume, setFinalVolume] = useState('600');
  const [doubleForHalfInWell, setDoubleForHalfInWell] = useState(false);

  const numberOfPoints = Number(points);
  const topConcentrationMicromolar = Number(topConcentration);
  const factorValue = Number(factor);
  const finalVolumeMicrolitres = Number(finalVolume);
  const columnValue = Number(column);

  const previewParams = useMemo<DilutionSchemeParams>(
    () => ({
      topConcentrationMicromolar,
      factor: factorValue,
      numberOfPoints,
      finalVolumeMicrolitres,
      doubleForHalfInWell,
    }),
    [topConcentrationMicromolar, factorValue, numberOfPoints, finalVolumeMicrolitres, doubleForHalfInWell],
  );

  const seriesValid =
    topConcentrationMicromolar > 0 && factorValue > 1 && numberOfPoints >= 1 && finalVolumeMicrolitres > 0;
  const columnValid = Number.isInteger(columnValue) && columnValue >= 1 && columnValue <= 12;

  const { data: preview } = useDilutionScheme(previewParams, seriesValid);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    try {
      await apply.mutateAsync({
        column: columnValue,
        topConcentrationMicromolar,
        factor: factorValue,
        numberOfPoints,
        finalVolumeMicrolitres,
        doubleForHalfInWell,
      });
      toast('success', `Coluna ${columnValue} preenchida a partir do esquema.`);
      onClose();
    } catch (err) {
      toast('error', (err as ApiError)?.message ?? 'Não foi possível aplicar o esquema à placa.');
    }
  }

  return (
    <Modal
      open
      onClose={onClose}
      size="lg"
      title="Aplicar diluição à placa"
      description="Preenche as concentrações de uma coluna (8 poços) a partir da série. A coluna deve ter o mesmo número de pontos."
      footer={
        <>
          <Button variant="outline" onClick={onClose} disabled={apply.isPending}>
            Cancelar
          </Button>
          <Button
            type="submit"
            form="apply-dilution-form"
            disabled={apply.isPending || !seriesValid || !columnValid}
          >
            {apply.isPending && <Loader2 className="size-4 animate-spin" />}
            Aplicar à coluna {columnValid ? columnValue : '—'}
          </Button>
        </>
      }
    >
      <form id="apply-dilution-form" className="space-y-5" onSubmit={handleSubmit} noValidate>
        <div className="grid grid-cols-2 gap-3">
          <div className="space-y-1.5">
            <Label htmlFor="apply-column">Coluna (1–12)</Label>
            <Input
              id="apply-column"
              type="number"
              min={1}
              max={12}
              step="1"
              value={column}
              onChange={(e) => setColumn(e.target.value)}
              required
            />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="apply-points">Nº de pontos</Label>
            <Input
              id="apply-points"
              type="number"
              min={1}
              step="1"
              value={points}
              onChange={(e) => setPoints(e.target.value)}
              required
            />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="apply-top">Concentração inicial (µM)</Label>
            <Input
              id="apply-top"
              type="number"
              min={0}
              step="any"
              value={topConcentration}
              onChange={(e) => setTopConcentration(e.target.value)}
              required
            />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="apply-factor">Fator</Label>
            <Input
              id="apply-factor"
              type="number"
              min={0}
              step="any"
              value={factor}
              onChange={(e) => setFactor(e.target.value)}
              required
            />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="apply-final">Volume final (µL)</Label>
            <Input
              id="apply-final"
              type="number"
              min={0}
              step="any"
              value={finalVolume}
              onChange={(e) => setFinalVolume(e.target.value)}
              required
            />
          </div>
        </div>

        <label className="flex items-start justify-between gap-4 rounded-md border bg-muted/30 p-3">
          <span>
            <span className="block text-sm font-medium">Metade no poço</span>
            <span className="block text-xs text-muted-foreground">
              Dobra a concentração da placa-mãe (diluição final no próprio poço).
            </span>
          </span>
          <Switch
            checked={doubleForHalfInWell}
            onCheckedChange={setDoubleForHalfInWell}
            aria-label="Metade no poço"
          />
        </label>

        {preview && (
          <div className="rounded-md border bg-muted/20 p-3">
            <p className="mb-2 text-xs font-medium uppercase tracking-wide text-muted-foreground">
              Série a aplicar
            </p>
            <div className="flex flex-wrap gap-1.5">
              {preview.steps.map((step) => (
                <span
                  key={step.index}
                  className="rounded-md border bg-background px-2 py-1 text-xs tabular-nums"
                >
                  {microMolarFormatter.format(step.concentrationMicromolar)} µM
                </span>
              ))}
            </div>
          </div>
        )}
      </form>
    </Modal>
  );
}
