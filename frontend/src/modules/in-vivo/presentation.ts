import type { BadgeProps } from '@/shared/components/ui/badge';
import type {
  AnalysisStatus,
  AnimalSex,
  BatchStatus,
  BehavioralType,
  PendencyKind,
  ProjectStatus,
  SampleType,
} from '@/modules/in-vivo/types';

type Variant = NonNullable<BadgeProps['variant']>;

/** Human label + badge variant for each project status. */
export const projectStatusPresentation: Record<
  ProjectStatus,
  { label: string; variant: Variant }
> = {
  Draft: { label: 'Rascunho', variant: 'muted' },
  Active: { label: 'Ativo', variant: 'default' },
  Closed: { label: 'Encerrado', variant: 'outline' },
};

/** Human label + badge variant for each batch status. */
export const batchStatusPresentation: Record<
  BatchStatus,
  { label: string; variant: Variant }
> = {
  Planned: { label: 'Planejada', variant: 'muted' },
  Running: { label: 'Em andamento', variant: 'secondary' },
  Completed: { label: 'Concluída', variant: 'default' },
};

/** Human label for each animal sex. */
export const animalSexLabel: Record<AnimalSex, string> = {
  Male: 'Macho',
  Female: 'Fêmea',
};

/** Human label + short description for each behavioural assay type. */
export const behavioralTypePresentation: Record<
  BehavioralType,
  { label: string; description: string; scorable: boolean }
> = {
  VonFrei: {
    label: 'Von Frey',
    description: 'Alodinia mecânica — limiar de retirada 50% (up-down Dixon/Chaplan).',
    scorable: true,
  },
  TailFlick: {
    label: 'Tail-flick',
    description: 'Latência de retirada da cauda ao estímulo térmico.',
    scorable: false,
  },
  RotaRod: {
    label: 'Rota-rod',
    description: 'Coordenação motora — tempo até a queda do cilindro rotatório.',
    scorable: false,
  },
  Hemograma: {
    label: 'Hemograma',
    description: 'Contagem sanguínea a partir da coleta.',
    scorable: false,
  },
};

/** Human label for each sample type. */
export const sampleTypeLabel: Record<SampleType, string> = {
  Blood: 'Sangue',
  Plasma: 'Plasma',
  Serum: 'Soro',
  Tissue: 'Tecido',
  CerebrospinalFluid: 'Líquor',
  Urine: 'Urina',
  Other: 'Outro',
};

/** Resolves a sample-type label from the raw backend enum name, falling back to the name itself. */
export function sampleTypeName(type: string): string {
  return sampleTypeLabel[type as SampleType] ?? type;
}

/** Human label + badge variant for each analysis status. */
export const analysisStatusPresentation: Record<
  AnalysisStatus,
  { label: string; variant: Variant }
> = {
  Pending: { label: 'Pendente', variant: 'secondary' },
  Completed: { label: 'Concluída', variant: 'default' },
};

/** Human label + badge variant for each pendency kind. */
export const pendencyKindPresentation: Record<
  PendencyKind,
  { label: string; variant: Variant }
> = {
  AwaitingCalculation: { label: 'Aguardando cálculo', variant: 'secondary' },
  PendingStep: { label: 'Etapa pendente', variant: 'muted' },
  SampleAwaitingAnalysis: { label: 'Amostra sem análise', variant: 'outline' },
};

/** Formats an ISO date string to the pt-BR short date, or a dash when null. */
export function formatDate(iso: string | null): string {
  if (!iso) return '—';
  const date = new Date(iso);
  return Number.isNaN(date.getTime()) ? '—' : date.toLocaleDateString('pt-BR');
}

/** Formats a decimal amount with its unit (e.g. "1.5 mL"), trimming trailing zeros. */
export function formatAmount(value: number, unit: string): string {
  return `${Number(value.toFixed(4))} ${unit}`;
}
