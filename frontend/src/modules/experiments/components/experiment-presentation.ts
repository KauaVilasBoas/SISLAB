import type { BadgeProps } from '@/shared/components/ui/badge';
import type { ExperimentStatus, ExperimentType, WellRole } from '@/modules/experiments/types';

/** Human label + badge variant for each experiment status. */
export const experimentStatusPresentation: Record<
  ExperimentStatus,
  { label: string; variant: NonNullable<BadgeProps['variant']> }
> = {
  Draft: { label: 'Rascunho', variant: 'muted' },
  InProgress: { label: 'Em andamento', variant: 'secondary' },
  AwaitingAnalysis: { label: 'Aguardando análise', variant: 'default' },
  Completed: { label: 'Concluído', variant: 'default' },
  Archived: { label: 'Arquivado', variant: 'outline' },
};

/** Human label + a Tailwind color class for each well role (used to tint the plate grid). */
export const wellRolePresentation: Record<
  WellRole,
  { label: string; cellClass: string }
> = {
  Control: { label: 'Controle', cellClass: 'bg-emerald-100 text-emerald-900 border-emerald-300' },
  Blank: { label: 'Branco', cellClass: 'bg-slate-100 text-slate-700 border-slate-300' },
  CurvePoint: { label: 'Curva', cellClass: 'bg-sky-100 text-sky-900 border-sky-300' },
  Sample: { label: 'Amostra', cellClass: 'bg-violet-100 text-violet-900 border-violet-300' },
  Standard: { label: 'Padrão', cellClass: 'bg-amber-100 text-amber-900 border-amber-300' },
};

/**
 * Per-type presentation: human label and how a well's computed value is rendered on the grid. Viability shows a
 * percentage; nitric oxide shows a µM concentration. Driven by the experiment type so the plate/detail views stay
 * type-agnostic and a new assay is one entry here.
 */
export const experimentTypePresentation: Record<
  ExperimentType,
  { label: string; description: string; formatComputed: (value: number) => string }
> = {
  ViabilidadeCelular: {
    label: 'Viabilidade celular',
    description: 'Ensaio in vitro de viabilidade celular (placa 8×12, MTT).',
    formatComputed: (value) => `${value}%`,
  },
  NitricOxide: {
    label: 'Óxido nítrico',
    description: 'Ensaio in vitro de óxido nítrico (placa 8×12, reação de Griess).',
    formatComputed: (value) => `${value} µM`,
  },
};

/** Resolves the type presentation, falling back to viability for an unknown/legacy type name. */
export function typePresentation(type: string) {
  return (
    experimentTypePresentation[type as ExperimentType] ??
    experimentTypePresentation.ViabilidadeCelular
  );
}

/** Formats an ISO date string to the pt-BR short date, or a dash when null. */
export function formatDate(iso: string | null): string {
  if (!iso) return '—';
  const date = new Date(iso);
  return Number.isNaN(date.getTime()) ? '—' : date.toLocaleDateString('pt-BR');
}
