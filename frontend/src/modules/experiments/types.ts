/**
 * Experiments module read/write contracts (card [E11] #68 — in vitro cell-viability slice).
 * Mirror of the backend DTOs returned by ExperimentsController; kept flat and primitive so the UI
 * binds to them directly without leaking any domain shape.
 */

/** Lifecycle status name as the backend serializes the enum. */
export type ExperimentStatus =
  'Draft' | 'InProgress' | 'AwaitingAnalysis' | 'Completed' | 'Archived';

/** Assay type discriminator name (in vitro plate assays; the in vivo behavioural ones live in the in-vivo module). */
export type ExperimentType = 'ViabilidadeCelular' | 'NitricOxide';

/** The behavioural (in vivo) assay type names the backend may return on a shared experiment detail. */
export const BEHAVIORAL_TYPES = ['VonFrei', 'TailFlick', 'RotaRod', 'Hemograma'] as const;

/** True when the experiment type name is one of the in vivo behavioural assays (not a plate assay). */
export function isBehavioralType(type: string): boolean {
  return (BEHAVIORAL_TYPES as readonly string[]).includes(type);
}

/** Well role name as the backend serializes the enum. */
export type WellRole = 'Control' | 'Blank' | 'CurvePoint' | 'Sample' | 'Standard';

/** A row on the experiments list. */
export interface ExperimentListItem {
  id: string;
  title: string;
  type: string;
  status: ExperimentStatus;
  isCalculated: boolean;
  createdAtUtc: string;
  createdBy: string;
}

/** A step in the experiment's execution flow. */
export interface ExperimentStepDetail {
  id: string;
  order: number;
  kind: string;
  title: string;
  performedBy: string | null;
  performedAtUtc: string | null;
  notes: string | null;
  /** Lumen user ids designated as responsible for this step (card [E11]). */
  responsibleUserIds: string[];
}

/** A designed well with its optional imported reading, on the detail. */
export interface PlateWellDetail {
  row: string;
  column: number;
  role: WellRole;
  concentrationUm: number | null;
  sampleId: string | null;
  rawAbsorbance: number | null;
}

/** The frozen calculation snapshot. */
export interface ExperimentCalculationDetail {
  formulaName: string;
  formulaExpression: string;
  appliedAtUtc: string;
  resultJson: string;
}

/** Full experiment detail. */
export interface ExperimentDetail {
  id: string;
  title: string;
  description: string | null;
  type: string;
  status: ExperimentStatus;
  compoundPartnerId: string | null;
  createdAtUtc: string;
  createdBy: string;
  /** Lead responsible's Lumen user id (card [E11]); null for experiments created before responsibility. */
  responsibleUserId: string | null;
  steps: ExperimentStepDetail[];
  wells: PlateWellDetail[];
  calculation: ExperimentCalculationDetail | null;
}

/** A cell on the plate-result grid. */
export interface PlateWellResult {
  row: string;
  column: number;
  role: WellRole;
  rawAbsorbance: number | null;
  /** The assay's computed value for this well (% viability or NO µM); null until calculated. */
  computedValue: number | null;
}

/** The plate-result grid. */
export interface PlateReadingResult {
  experimentId: string;
  isCalculated: boolean;
  wells: PlateWellResult[];
}

/** Request body to create a plate experiment (viability or nitric oxide). */
export interface CreateExperimentRequest {
  type: ExperimentType;
  title: string;
  description: string | null;
  compoundPartnerId: string | null;
}

/** One well in a plate-design request. */
export interface DesignPlateWellRequest {
  row: string;
  column: number;
  role: WellRole;
  concentrationUm: number | null;
  sampleId: string | null;
}

// ---------------------------------------------------------------------------
// Serial dilution scheme (SISLAB-05 — mother-plate calculator)
// ---------------------------------------------------------------------------

/**
 * Query parameters for the stateless serial-dilution compute (GET /experiments/dilution-scheme).
 * The series inputs (top concentration, factor, points, final volume, half-in-well) are always sent; the
 * stock and DMSO groups are optional (present only when the operator wants those controls computed too).
 */
export interface DilutionSchemeParams {
  topConcentrationMicromolar: number;
  factor: number;
  numberOfPoints: number;
  finalVolumeMicrolitres: number;
  doubleForHalfInWell?: boolean;
  /** Stock via V = m × M / MM — molar-mass route (the three go together). */
  stockMolarMassGramsPerMole?: number;
  stockMassMilligrams?: number;
  stockTargetMolarityMicromolar?: number;
  /** Stock via a plain mg/mL pesagem — no molar mass (the two go together). */
  stockConcentrationMilligramsPerMillilitre?: number;
  stockVolumeMillilitres?: number;
  /** DMSO control (both go together). */
  dmsoMicrolitres?: number;
  dmsoSolutionMicrolitres?: number;
  /** In-well further dilution of the mother solution (1 = neat). */
  dmsoInWellDilutionRatio?: number;
}

/** One point of the computed series: concentration + how to reach it (C1V1=C2V2). */
export interface DilutionSchemeStep {
  index: number;
  concentrationMicromolar: number;
  transferMicrolitres: number | null;
  diluentMicrolitres: number | null;
  finalVolumeMicrolitres: number;
}

/** The stock solution the series steps down from, when a compound was supplied. */
export interface DilutionStock {
  massMilligrams: number;
  molarMassGramsPerMole: number | null;
  concentrationMicromolar: number | null;
  concentrationMilligramsPerMillilitre: number;
  volumeMillilitres: number;
}

/** The DMSO control: the solvent fraction in the mother solution and once in the well. */
export interface DilutionDmso {
  dmsoMicrolitres: number;
  solutionMicrolitres: number;
  solutionFraction: number;
  wellFraction: number;
}

/** The computed serial-dilution scheme (SISLAB-05). Mirror of the backend SerialDilutionSchemeResult. */
export interface DilutionScheme {
  factor: number;
  finalVolumeMicrolitres: number;
  steps: DilutionSchemeStep[];
  stock: DilutionStock | null;
  dmso: DilutionDmso | null;
}

/**
 * Request body to populate a plate column's concentrations from a serial-dilution scheme
 * (POST /experiments/{id}/apply-dilution-scheme). The series inputs are recomputed server-side.
 */
export interface ApplyDilutionSchemeRequest {
  column: number;
  topConcentrationMicromolar: number;
  factor: number;
  numberOfPoints: number;
  finalVolumeMicrolitres: number;
  doubleForHalfInWell?: boolean;
}

/**
 * Request body to assign a responsible (card [E11]) — the lead of an experiment
 * (PUT /experiments/{id}/responsible) or a responsible on a step
 * (POST /experiments/{id}/steps/{stepId}/responsibles). Carries the target member's Lumen user id.
 */
export interface AssignResponsibleRequest {
  responsibleUserId: string;
}
