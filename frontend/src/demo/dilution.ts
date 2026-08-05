import type {
  DilutionDmso,
  DilutionScheme,
  DilutionSchemeStep,
  DilutionStock,
} from '@/modules/experiments/types';

/**
 * Faithful port of the backend serial-dilution math (SISLAB-05) for the backend-less demo, so the
 * calculator shows the REAL numbers with no server. Mirrors, line for line:
 *  - Domain/Preparations/SerialDilutionCalculator.cs  (series + C1V1=C2V2 transfer/diluent)
 *  - Domain/Preparations/StockSolution.cs             (V = m·M/MM and the mg/mL route)
 *  - Domain/Preparations/DmsoDilution.cs              (solvent fractions)
 *
 * All inputs are positive here (the caller only fires when the series is valid), so rounding
 * half-up (Math.round) matches the backend's MidpointRounding.AwayFromZero.
 */

function num(sp: URLSearchParams, key: string): number | undefined {
  const raw = sp.get(key);
  if (raw === null || raw === '') return undefined;
  const parsed = Number(raw);
  return Number.isFinite(parsed) ? parsed : undefined;
}

function round(value: number, decimals: number): number {
  const factor = 10 ** decimals;
  return Math.round(value * factor) / factor;
}

/** Computes the full DilutionScheme from the query string the SPA sends (GET /api/experiments/dilution-scheme). */
export function computeDilutionScheme(sp: URLSearchParams): DilutionScheme {
  const topConcentration = num(sp, 'topConcentrationMicromolar') ?? 0;
  const factor = num(sp, 'factor') ?? 0;
  const numberOfPoints = num(sp, 'numberOfPoints') ?? 0;
  const finalVolume = num(sp, 'finalVolumeMicrolitres') ?? 0;
  const doubleForHalfInWell = sp.get('doubleForHalfInWell') === 'true';

  const top = doubleForHalfInWell ? topConcentration * 2 : topConcentration;
  const transfer = round(finalVolume / factor, 2);
  const diluent = round(finalVolume - transfer, 2);

  const steps: DilutionSchemeStep[] = [];
  let concentration = top;
  for (let index = 0; index < numberOfPoints; index++) {
    const isTop = index === 0;
    steps.push({
      index,
      concentrationMicromolar: round(concentration, 3),
      transferMicrolitres: isTop ? null : transfer,
      diluentMicrolitres: isTop ? null : diluent,
      finalVolumeMicrolitres: finalVolume,
    });
    concentration /= factor;
  }

  return {
    factor,
    finalVolumeMicrolitres: finalVolume,
    steps,
    stock: computeStock(sp),
    dmso: computeDmso(sp),
  };
}

/** Optional stock solution: molar-mass route (V = m·M/MM) when the triple is present, else the mg/mL route. */
function computeStock(sp: URLSearchParams): DilutionStock | null {
  const molarMass = num(sp, 'stockMolarMassGramsPerMole');
  const mass = num(sp, 'stockMassMilligrams');
  const molarity = num(sp, 'stockTargetMolarityMicromolar');
  if (molarMass !== undefined && mass !== undefined && molarity !== undefined) {
    const micromoles = (mass / molarMass) * 1000;
    const volumeMillilitres = (micromoles / molarity) * 1000;
    return {
      massMilligrams: mass,
      molarMassGramsPerMole: molarMass,
      concentrationMicromolar: molarity,
      concentrationMilligramsPerMillilitre: mass / volumeMillilitres,
      volumeMillilitres,
    };
  }

  const mgPerMl = num(sp, 'stockConcentrationMilligramsPerMillilitre');
  const volume = num(sp, 'stockVolumeMillilitres');
  if (mgPerMl !== undefined && volume !== undefined) {
    return {
      massMilligrams: mgPerMl * volume,
      molarMassGramsPerMole: null,
      concentrationMicromolar: null,
      concentrationMilligramsPerMillilitre: mgPerMl,
      volumeMillilitres: volume,
    };
  }

  return null;
}

/** Optional DMSO control: the solvent fraction in the mother solution and once in the well. */
function computeDmso(sp: URLSearchParams): DilutionDmso | null {
  const dmso = num(sp, 'dmsoMicrolitres');
  const solution = num(sp, 'dmsoSolutionMicrolitres');
  if (dmso === undefined || solution === undefined) return null;

  const ratio = num(sp, 'dmsoInWellDilutionRatio') ?? 1;
  const solutionFraction = dmso / solution;
  const wellFraction = solutionFraction / ratio;
  return {
    dmsoMicrolitres: dmso,
    solutionMicrolitres: solution,
    solutionFraction: round(solutionFraction, 6),
    wellFraction: round(wellFraction, 6),
  };
}
