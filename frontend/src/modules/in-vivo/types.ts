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

export interface AnimalDetail {
  id: string;
  identifier: string;
  sex: AnimalSex;
  weightGrams: number | null;
}

export interface GroupDetail {
  id: string;
  name: string;
  doseAmount: number;
  doseUnit: string;
  animals: AnimalDetail[];
}

export interface BatchDetail {
  id: string;
  name: string;
  designVersion: number;
  status: BatchStatus;
  groups: GroupDetail[];
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

export interface AddAnimalRequest {
  identifier: string;
  sex: AnimalSex;
  weightGrams: number | null;
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
