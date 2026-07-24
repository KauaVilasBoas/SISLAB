import { useEffect, useMemo, useState } from 'react';
import { Loader2, Beaker, AlertCircle } from 'lucide-react';
import { PageHeader } from '@/shared/components/PageHeader';
import { Input } from '@/shared/components/ui/input';
import { Label } from '@/shared/components/ui/label';
import { Switch } from '@/shared/components/ui/switch';
import { Badge } from '@/shared/components/ui/badge';
import { useDilutionScheme } from '@/modules/experiments/api/dilution.queries';
import type { DilutionSchemeParams } from '@/modules/experiments/types';

/** Small controlled-value debounce so the compute fires while the operator types, not on every keystroke. */
function useDebouncedValue<T>(value: T, delayMs: number): T {
  const [debounced, setDebounced] = useState(value);
  useEffect(() => {
    const timer = window.setTimeout(() => setDebounced(value), delayMs);
    return () => window.clearTimeout(timer);
  }, [value, delayMs]);
  return debounced;
}

/** Parses a text field to a positive number, or undefined when blank/invalid — the shape the params expect. */
function toNumber(value: string): number | undefined {
  if (value.trim() === '') return undefined;
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : undefined;
}

const microlitreFormatter = new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 2 });
const microMolarFormatter = new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 4 });
const percentFormatter = new Intl.NumberFormat('pt-BR', {
  style: 'percent',
  maximumFractionDigits: 2,
});

function ul(value: number | null | undefined): string {
  return value === null || value === undefined ? '—' : `${microlitreFormatter.format(value)} µL`;
}

/**
 * Serial-dilution calculator (SISLAB-05): the antidote to the by-hand mother-plate column of the in vitro
 * spreadsheet. The operator sets the top concentration, factor, number of points and final volume; the page computes
 * the series and the C1V1=C2V2 transfer/diluent volumes as they type (debounced GET, stateless). Optional panels add
 * the stock solution (V = m × M / MM or a mg/mL pesagem) and the DMSO control. Every laboratory-specific value is an
 * input — nothing (factor, volume, range) is hardcoded. Applying a scheme to a real plate lives on the experiment
 * detail (permission-gated); this page is the pure calculator any member may use.
 */
export function DilutionCalculatorPage() {
  // Series inputs (the planilha's default: 200 µM, factor 4, 6 points, 600 µL).
  const [topConcentration, setTopConcentration] = useState('200');
  const [factor, setFactor] = useState('4');
  const [points, setPoints] = useState('6');
  const [finalVolume, setFinalVolume] = useState('600');
  const [doubleForHalfInWell, setDoubleForHalfInWell] = useState(false);

  // Optional stock (molar-mass route: mass + MM + target µM).
  const [showStock, setShowStock] = useState(false);
  const [stockMass, setStockMass] = useState('');
  const [molarMass, setMolarMass] = useState('');
  const [stockTargetMolarity, setStockTargetMolarity] = useState('');
  // Optional stock (mg/mL route: no molar mass).
  const [stockMgPerMl, setStockMgPerMl] = useState('');
  const [stockVolumeMl, setStockVolumeMl] = useState('');

  // Optional DMSO control.
  const [showDmso, setShowDmso] = useState(false);
  const [dmsoMicrolitres, setDmsoMicrolitres] = useState('');
  const [dmsoSolution, setDmsoSolution] = useState('');
  const [dmsoInWellRatio, setDmsoInWellRatio] = useState('1');

  const params = useMemo<DilutionSchemeParams>(
    () => ({
      topConcentrationMicromolar: toNumber(topConcentration) ?? 0,
      factor: toNumber(factor) ?? 0,
      numberOfPoints: toNumber(points) ?? 0,
      finalVolumeMicrolitres: toNumber(finalVolume) ?? 0,
      doubleForHalfInWell,
      stockMolarMassGramsPerMole: showStock ? toNumber(molarMass) : undefined,
      stockMassMilligrams: showStock ? toNumber(stockMass) : undefined,
      stockTargetMolarityMicromolar: showStock ? toNumber(stockTargetMolarity) : undefined,
      stockConcentrationMilligramsPerMillilitre: showStock ? toNumber(stockMgPerMl) : undefined,
      stockVolumeMillilitres: showStock ? toNumber(stockVolumeMl) : undefined,
      dmsoMicrolitres: showDmso ? toNumber(dmsoMicrolitres) : undefined,
      dmsoSolutionMicrolitres: showDmso ? toNumber(dmsoSolution) : undefined,
      dmsoInWellDilutionRatio: showDmso ? toNumber(dmsoInWellRatio) : undefined,
    }),
    [
      topConcentration,
      factor,
      points,
      finalVolume,
      doubleForHalfInWell,
      showStock,
      molarMass,
      stockMass,
      stockTargetMolarity,
      stockMgPerMl,
      stockVolumeMl,
      showDmso,
      dmsoMicrolitres,
      dmsoSolution,
      dmsoInWellRatio,
    ],
  );

  const debouncedParams = useDebouncedValue(params, 350);

  // Mirror the backend validator so we never fire a request the server would 422: concentration/volume > 0,
  // factor > 1, at least one point.
  const isValid =
    debouncedParams.topConcentrationMicromolar > 0 &&
    debouncedParams.factor > 1 &&
    debouncedParams.numberOfPoints >= 1 &&
    debouncedParams.finalVolumeMicrolitres > 0;

  const { data: scheme, isFetching, isError } = useDilutionScheme(debouncedParams, isValid);

  return (
    <div className="space-y-6">
      <PageHeader
        title="Diluição seriada"
        description="Calculadora da placa-mãe in vitro — série, volumes a pipetar (C1V1=C2V2), estoque e DMSO. Aplique um esquema a uma placa pela tela do experimento."
      />

      <div className="grid gap-6 lg:grid-cols-[360px_1fr]">
        {/* Inputs */}
        <div className="space-y-4">
          <section className="space-y-4 rounded-lg border bg-card p-5">
            <h2 className="text-sm font-semibold uppercase tracking-wide text-muted-foreground">
              Série
            </h2>
            <div className="grid grid-cols-2 gap-3">
              <Field
                id="dl-top"
                label="Concentração inicial (µM)"
                value={topConcentration}
                onChange={setTopConcentration}
                placeholder="200"
              />
              <Field
                id="dl-factor"
                label="Fator"
                value={factor}
                onChange={setFactor}
                placeholder="4"
              />
              <Field
                id="dl-points"
                label="Nº de pontos"
                value={points}
                onChange={setPoints}
                placeholder="6"
                step="1"
              />
              <Field
                id="dl-final"
                label="Volume final (µL)"
                value={finalVolume}
                onChange={setFinalVolume}
                placeholder="600"
              />
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
          </section>

          {/* Optional stock */}
          <section className="space-y-4 rounded-lg border bg-card p-5">
            <label className="flex items-center justify-between gap-4">
              <span className="text-sm font-semibold uppercase tracking-wide text-muted-foreground">
                Solução estoque
              </span>
              <Switch
                checked={showStock}
                onCheckedChange={setShowStock}
                aria-label="Calcular solução estoque"
              />
            </label>
            {showStock && (
              <div className="space-y-4">
                <p className="text-xs text-muted-foreground">
                  Com massa molar: V = m × M / MM. Sem massa molar: informe mg/mL e o volume.
                </p>
                <div className="grid grid-cols-2 gap-3">
                  <Field
                    id="dl-mass"
                    label="Massa pesada (mg)"
                    value={stockMass}
                    onChange={setStockMass}
                    placeholder="Ex.: 1"
                  />
                  <Field
                    id="dl-mm"
                    label="Massa molar (g/mol)"
                    value={molarMass}
                    onChange={setMolarMass}
                    placeholder="Ex.: 612.716"
                  />
                  <Field
                    id="dl-target"
                    label="Molaridade alvo (µM)"
                    value={stockTargetMolarity}
                    onChange={setStockTargetMolarity}
                    placeholder="Ex.: 10000"
                  />
                </div>
                <div className="grid grid-cols-2 gap-3 border-t pt-3">
                  <Field
                    id="dl-mgml"
                    label="Concentração (mg/mL)"
                    value={stockMgPerMl}
                    onChange={setStockMgPerMl}
                    placeholder="sem massa molar"
                  />
                  <Field
                    id="dl-stockvol"
                    label="Volume (mL)"
                    value={stockVolumeMl}
                    onChange={setStockVolumeMl}
                    placeholder="Ex.: 1"
                  />
                </div>
              </div>
            )}
          </section>

          {/* Optional DMSO */}
          <section className="space-y-4 rounded-lg border bg-card p-5">
            <label className="flex items-center justify-between gap-4">
              <span className="text-sm font-semibold uppercase tracking-wide text-muted-foreground">
                Controle de DMSO
              </span>
              <Switch
                checked={showDmso}
                onCheckedChange={setShowDmso}
                aria-label="Calcular controle de DMSO"
              />
            </label>
            {showDmso && (
              <div className="grid grid-cols-2 gap-3">
                <Field
                  id="dl-dmso"
                  label="DMSO puro (µL)"
                  value={dmsoMicrolitres}
                  onChange={setDmsoMicrolitres}
                  placeholder="Ex.: 300"
                />
                <Field
                  id="dl-dmso-sol"
                  label="Solução total (µL)"
                  value={dmsoSolution}
                  onChange={setDmsoSolution}
                  placeholder="Ex.: 1500"
                />
                <Field
                  id="dl-dmso-well"
                  label="Diluição no poço"
                  value={dmsoInWellRatio}
                  onChange={setDmsoInWellRatio}
                  placeholder="1"
                />
              </div>
            )}
          </section>
        </div>

        {/* Results */}
        <div className="space-y-4">
          {!isValid ? (
            <div className="flex items-start gap-2 rounded-lg border border-dashed bg-card p-8 text-sm text-muted-foreground">
              <AlertCircle className="mt-0.5 size-4 shrink-0" />
              Preencha a série (concentração &gt; 0, fator &gt; 1, pontos ≥ 1 e volume &gt; 0) para ver o esquema.
            </div>
          ) : isError ? (
            <p className="rounded-lg border bg-card p-8 text-center text-sm text-destructive">
              Não foi possível calcular o esquema. Revise os valores informados.
            </p>
          ) : !scheme ? (
            <div className="flex items-center justify-center gap-2 rounded-lg border bg-card p-8 text-sm text-muted-foreground">
              <Loader2 className="size-4 animate-spin" />
              Calculando…
            </div>
          ) : (
            <>
              <section className="rounded-lg border bg-card">
                <div className="flex flex-wrap items-center justify-between gap-2 border-b p-4">
                  <div className="flex items-center gap-2">
                    <Beaker className="size-4 text-muted-foreground" />
                    <h2 className="text-sm font-semibold uppercase tracking-wide text-muted-foreground">
                      Série
                    </h2>
                    {isFetching && <Loader2 className="size-3.5 animate-spin text-muted-foreground" />}
                  </div>
                  <div className="flex items-center gap-2">
                    <Badge variant="secondary">fator {microMolarFormatter.format(scheme.factor)}</Badge>
                    <Badge variant="outline">{ul(scheme.finalVolumeMicrolitres)} final</Badge>
                  </div>
                </div>
                <table className="w-full text-sm">
                  <thead className="border-b text-left text-xs uppercase tracking-wide text-muted-foreground">
                    <tr>
                      <th className="px-4 py-2.5 font-medium">Ponto</th>
                      <th className="px-4 py-2.5 font-medium">Concentração</th>
                      <th className="px-4 py-2.5 font-medium">Transferir</th>
                      <th className="px-4 py-2.5 font-medium">Diluente</th>
                      <th className="px-4 py-2.5 font-medium">Volume</th>
                    </tr>
                  </thead>
                  <tbody>
                    {scheme.steps.map((step) => (
                      <tr key={step.index} className="border-b last:border-0">
                        <td className="px-4 py-2.5 text-muted-foreground">{step.index + 1}</td>
                        <td className="px-4 py-2.5 font-medium tabular-nums">
                          {microMolarFormatter.format(step.concentrationMicromolar)} µM
                        </td>
                        <td className="px-4 py-2.5 tabular-nums">{ul(step.transferMicrolitres)}</td>
                        <td className="px-4 py-2.5 tabular-nums">{ul(step.diluentMicrolitres)}</td>
                        <td className="px-4 py-2.5 tabular-nums text-muted-foreground">
                          {ul(step.finalVolumeMicrolitres)}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </section>

              {scheme.stock && (
                <section className="rounded-lg border bg-card p-5">
                  <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-muted-foreground">
                    Solução estoque
                  </h2>
                  <dl className="grid grid-cols-2 gap-x-6 gap-y-2 text-sm sm:grid-cols-3">
                    <Metric label="Massa" value={`${microMolarFormatter.format(scheme.stock.massMilligrams)} mg`} />
                    <Metric
                      label="Volume"
                      value={`${microMolarFormatter.format(scheme.stock.volumeMillilitres)} mL`}
                    />
                    <Metric
                      label="mg/mL"
                      value={microMolarFormatter.format(scheme.stock.concentrationMilligramsPerMillilitre)}
                    />
                    {scheme.stock.molarMassGramsPerMole != null && (
                      <Metric
                        label="Massa molar"
                        value={`${microMolarFormatter.format(scheme.stock.molarMassGramsPerMole)} g/mol`}
                      />
                    )}
                    {scheme.stock.concentrationMicromolar != null && (
                      <Metric
                        label="Molaridade"
                        value={`${microMolarFormatter.format(scheme.stock.concentrationMicromolar)} µM`}
                      />
                    )}
                  </dl>
                </section>
              )}

              {scheme.dmso && (
                <section className="rounded-lg border bg-card p-5">
                  <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-muted-foreground">
                    Controle de DMSO
                  </h2>
                  <dl className="grid grid-cols-2 gap-x-6 gap-y-2 text-sm sm:grid-cols-4">
                    <Metric label="DMSO puro" value={ul(scheme.dmso.dmsoMicrolitres)} />
                    <Metric label="Solução" value={ul(scheme.dmso.solutionMicrolitres)} />
                    <Metric label="Fração na solução" value={percentFormatter.format(scheme.dmso.solutionFraction)} />
                    <Metric label="Fração no poço" value={percentFormatter.format(scheme.dmso.wellFraction)} />
                  </dl>
                </section>
              )}
            </>
          )}
        </div>
      </div>
    </div>
  );
}

/** A labelled numeric text field — thin wrapper over <Input> for the calculator's dense grid. */
function Field({
  id,
  label,
  value,
  onChange,
  placeholder,
  step = 'any',
}: {
  id: string;
  label: string;
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
  step?: string;
}) {
  return (
    <div className="space-y-1.5">
      <Label htmlFor={id}>{label}</Label>
      <Input
        id={id}
        type="number"
        min={0}
        step={step}
        inputMode="decimal"
        value={value}
        onChange={(e) => onChange(e.target.value)}
        placeholder={placeholder}
      />
    </div>
  );
}

/** A read-only labelled metric on a result panel. */
function Metric({ label, value }: { label: string; value: string }) {
  return (
    <div className="space-y-0.5">
      <dt className="text-xs text-muted-foreground">{label}</dt>
      <dd className="font-medium tabular-nums">{value}</dd>
    </div>
  );
}
