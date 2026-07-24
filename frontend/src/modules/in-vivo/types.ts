/**
 * In vivo module read/write contracts (cards [E11] #73/#88/#89/#90/#31): the experimental design
 * (Project → Batch → Group → Animal), the behavioural timepoint launch, the biobank (Sample/Analysis
 * with a derived balance) and the pendencies panel. Mirror of the backend DTOs — flat and primitive so
 * the UI binds directly without leaking any domain shape.
 */

// ---------------------------------------------------------------------------
// Projects (delineation)
// ---------------------------------------------------------------------------

export type ProjectStatus = 'Draft' | 'Active' | 'Closed';
export type BatchStatus = 'Planned' | 'Running' | 'Completed';
export type AnimalSex = 'Male' | 'Female';

/** A row on the projects list. */
export interface ProjectListItem {
  id: string;
  name: string;
  species: string;
  status: ProjectStatus;
  designVersion: number;
  batchCount: number;
  animalCount: number;
}

/**
 * An animal housed in a cage (SISLAB-03). Its treatment group is an optional assignment made after
 * basal/induction: `groupId` is null while the animal is still unassigned (pre-randomization).
 */
export interface AnimalDetail {
  id: string;
  identifier: string;
  sex: AnimalSex;
  weightGrams: number | null;
  /** The assigned treatment group (dose), or null while unassigned (pre-randomization). */
  groupId: string | null;
}

/** A dose group (treatment definition) of a batch — the arm an animal is assigned to after basal. */
export interface GroupDetail {
  id: string;
  name: string;
  doseAmount: number;
  doseUnit: string;
}

/** A cage (caixa) housing animals in a batch (SISLAB-03). Capacity (e.g. 4) is a per-lab parameter. */
export interface CageDetail {
  id: string;
  name: string;
  capacity: number | null;
  animals: AnimalDetail[];
}

export interface BatchDetail {
  id: string;
  name: string;
  designVersion: number;
  status: BatchStatus;
  /** The bound experimental model / induction protocol (SISLAB-04), or null while unbound. */
  experimentalModelId: string | null;
  /** The dose groups (treatment definitions) animals are assigned to after basal. */
  groups: GroupDetail[];
  /** The cages (caixas) housing the batch's animals (SISLAB-03). */
  cages: CageDetail[];
}

/** Request body to bind a batch (leva) to an experimental model (SISLAB-04). */
export interface BindBatchModelRequest {
  experimentalModelId: string;
}

export interface ProjectDetail {
  id: string;
  name: string;
  species: string;
  description: string | null;
  status: ProjectStatus;
  currentDesignVersion: number;
  batches: BatchDetail[];
}

export interface CreateProjectRequest {
  name: string;
  species: string;
  description: string | null;
}

export interface AddGroupRequest {
  name: string;
  doseAmount: number;
  doseUnit: string;
}

/** Request body to add a cage (caixa) to a batch (SISLAB-03); capacity (e.g. 4) is optional. */
export interface AddCageRequest {
  name: string;
  capacity: number | null;
}

/**
 * Request body to house an animal in a cage (SISLAB-03). `groupId` is optional: omit it for the
 * pre-randomization flow (assign later), or supply it to assign the group at entry.
 */
export interface AddAnimalRequest {
  identifier: string;
  sex: AnimalSex;
  weightGrams: number | null;
  groupId?: string;
}

/** Request body to assign (or move) an animal to a treatment group after basal (SISLAB-03). */
export interface AssignAnimalToGroupRequest {
  groupId: string;
}

/**
 * Per-cage baseline summary of one physiological parameter (SISLAB-03) — the pre-randomization view
 * (mean/min/max the researcher exports to Prism). An unmeasured cage still appears with a zero count.
 */
export interface CageBaselineItem {
  cageId: string;
  cageName: string;
  animalsWithReading: number;
  meanValue: number | null;
  minValue: number | null;
  maxValue: number | null;
  unit: string | null;
}

/**
 * Per-group baseline summary of one physiological parameter (SISLAB-03) — the post-randomization
 * balance-check view. Only assigned animals participate; an empty group still appears with a zero count.
 */
export interface GroupBaselineItem {
  groupId: string;
  groupName: string;
  doseAmount: number;
  doseUnit: string;
  animalsWithReading: number;
  meanValue: number | null;
  minValue: number | null;
  maxValue: number | null;
  unit: string | null;
}

// ---------------------------------------------------------------------------
// Solution preparation (SISLAB-01 — dose × weight, density, g:µL relation)
// ---------------------------------------------------------------------------

/** Physical state of the compound as the backend serializes the enum. Drives whether density is required. */
export type CompoundState = 'Powder' | 'Liquid';

/**
 * Request body to confirm a dose group's in vivo solution preparation (SISLAB-01).
 * Controle (só veículo): `isVehicleOnly = true` + the relation + the weight basis, no compound fields.
 * Treatment arm: dose + group weight + state (+ density when Liquid).
 */
export interface PrepareGroupSolutionRequest {
  isVehicleOnly: boolean;
  relationMicrolitresPerGram: number;
  relationWeightGrams: number;
  doseAmountGramsPerKilogram?: number;
  groupWeightGrams?: number;
  state?: CompoundState;
  densityGramsPerMillilitre?: number;
}

/** A confirmed, frozen solution preparation snapshot with its inputs and computed volumes (SISLAB-01). */
export interface SolutionPreparationListItem {
  id: string;
  batchId: string;
  groupId: string;
  groupName: string;
  isVehicleOnly: boolean;
  doseAmountGramsPerKilogram: number;
  groupWeightGrams: number;
  relationWeightGrams: number;
  relationMicrolitresPerGram: number;
  compoundState: string;
  densityGramsPerMillilitre: number | null;
  compoundMassGrams: number;
  compoundVolumeMicrolitres: number | null;
  finalVolumeMicrolitres: number;
  diluentVolumeMicrolitres: number;
  formulaCode: string;
  preparedBy: string;
  preparedAtUtc: string;
}

// ---------------------------------------------------------------------------
// Behavioural experiments (timepoint launch)
// ---------------------------------------------------------------------------

export type BehavioralType = 'VonFrei' | 'TailFlick' | 'RotaRod' | 'Hemograma';

export interface CreateBehavioralExperimentRequest {
  type: BehavioralType;
  title: string;
  description: string | null;
  projectId: string;
  batchId: string;
  timepointLabels: string[];
}

export interface TimepointReadingRequest {
  animalId: string;
  rawValue: string;
}

export interface RecordTimepointRequest {
  timepointLabel: string;
  readings: TimepointReadingRequest[];
}

// ---------------------------------------------------------------------------
// Biobank (samples / analyses)
// ---------------------------------------------------------------------------

export type SampleType =
  'Blood' | 'Plasma' | 'Serum' | 'Tissue' | 'CerebrospinalFluid' | 'Urine' | 'Other';

export type AnalysisStatus = 'Pending' | 'Completed';

/** A row on the samples list, with the derived remaining balance. */
export interface SampleListItem {
  id: string;
  code: string;
  type: string;
  animalId: string;
  sourceExperimentId: string;
  collectedQuantity: number;
  consumedQuantity: number;
  remainingQuantity: number;
  unit: string;
  analysisCount: number;
  collectedAtUtc: string;
}

export interface SampleAnalysisDetail {
  id: string;
  name: string;
  consumedQuantity: number;
  unit: string;
  status: AnalysisStatus;
  result: string | null;
  performedBy: string;
  performedAtUtc: string;
}

export interface SampleDetail {
  id: string;
  code: string;
  type: string;
  projectId: string;
  batchId: string;
  animalId: string;
  sourceExperimentId: string;
  collectedQuantity: number;
  consumedQuantity: number;
  remainingQuantity: number;
  unit: string;
  conservationTempMinCelsius: number | null;
  conservationTempMaxCelsius: number | null;
  storageLabel: string | null;
  notes: string | null;
  collectedBy: string;
  collectedAtUtc: string;
  analyses: SampleAnalysisDetail[];
}

export interface CollectSampleRequest {
  sourceExperimentId: string;
  animalId: string;
  code: string;
  type: SampleType;
  quantity: number;
  unit: string;
  conservationTempMinCelsius: number | null;
  conservationTempMaxCelsius: number | null;
  storageLabel: string | null;
  notes: string | null;
}

export interface AnalyseSampleRequest {
  name: string;
  consumedQuantity: number;
  unit: string;
}

export interface RecordAnalysisResultRequest {
  result: string;
}

// ---------------------------------------------------------------------------
// Pendencies panel
// ---------------------------------------------------------------------------

export type PendencyKind =
  'AwaitingCalculation' | 'PendingStep' | 'SampleAwaitingAnalysis';

export interface PendencyItem {
  kind: PendencyKind;
  referenceId: string;
  title: string;
  detail: string;
  sinceUtc: string;
}

export interface PendenciesResult {
  items: PendencyItem[];
  awaitingCalculationCount: number;
  pendingStepCount: number;
  sampleAwaitingAnalysisCount: number;
}
