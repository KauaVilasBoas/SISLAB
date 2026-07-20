import type {
  AgendaActivity,
  AgendaActivityType,
  AssignmentStatus,
  PresentationStatus,
  PresentationType,
  RoomType,
} from './types';

export const ROOM_TYPE_LABEL: Record<RoomType, string> = {
  Lab: 'Laboratório',
  OperatingRoom: 'Sala cirúrgica',
  Vivarium: 'Biotério',
  Office: 'Escritório',
  Meeting: 'Reunião',
};

export const ACTIVITY_LABEL: Record<AgendaActivity, string> = {
  VonFrey: 'Von Frey',
  Hargreaves: 'Hargreaves',
  TailFlick: 'Tail Flick',
  RotaRod: 'Rota-Rod',
  Hemogram: 'Hemograma',
  Dissection: 'Dissecção',
  Surgery: 'Cirurgia',
  AnimalCare: 'Manejo animal',
  Meeting: 'Reunião',
  Other: 'Outro',
};

export const ASSIGNMENT_STATUS_LABEL: Record<AssignmentStatus, string> = {
  Pending: 'Pendente',
  Done: 'Realizado',
  Swapped: 'Permutado',
};

export const ASSIGNMENT_STATUS_VARIANT: Record<AssignmentStatus, 'default' | 'secondary' | 'muted' | 'outline'> = {
  Pending: 'secondary',
  Done: 'default',
  Swapped: 'muted',
};

export const PRESENTATION_TYPE_LABEL: Record<PresentationType, string> = {
  Article: 'Artigo',
  Lecture: 'Aula',
  Preview: 'Prévia',
  Editorial: 'Editorial',
  DolAlert: 'Alerta DOL',
};

export const PRESENTATION_STATUS_LABEL: Record<PresentationStatus, string> = {
  Scheduled: 'Agendado',
  Done: 'Realizado',
  Cancelled: 'Cancelado',
};

export const PRESENTATION_STATUS_VARIANT: Record<PresentationStatus, 'default' | 'secondary' | 'muted' | 'outline'> = {
  Scheduled: 'secondary',
  Done: 'default',
  Cancelled: 'outline',
};

// ---------------------------------------------------------------------------
// Improved calendar — AgendaActivityType presentation (cards [E10.5-7])
// ---------------------------------------------------------------------------

export const ACTIVITY_TYPE_LABEL: Record<AgendaActivityType, string> = {
  RoomBooking: 'Reserva de sala',
  Experiment: 'Experimento',
  Bioterium: 'Biotério',
  Presentation: 'Apresentação',
  Other: 'Outro',
};

/**
 * Per-activity-type colour tokens for the calendar event chips. Each entry keeps the accent, a soft
 * background, a border and a solid dot so the same palette drives both the event block and the legend.
 */
export const ACTIVITY_TYPE_COLOR: Record<
  AgendaActivityType,
  { bg: string; border: string; text: string; dot: string }
> = {
  RoomBooking: {
    bg: 'bg-blue-500/10',
    border: 'border-blue-500/40',
    text: 'text-blue-700 dark:text-blue-300',
    dot: 'bg-blue-500',
  },
  Experiment: {
    bg: 'bg-emerald-500/10',
    border: 'border-emerald-500/40',
    text: 'text-emerald-700 dark:text-emerald-300',
    dot: 'bg-emerald-500',
  },
  Bioterium: {
    bg: 'bg-amber-500/10',
    border: 'border-amber-500/40',
    text: 'text-amber-700 dark:text-amber-300',
    dot: 'bg-amber-500',
  },
  Presentation: {
    bg: 'bg-purple-500/10',
    border: 'border-purple-500/40',
    text: 'text-purple-700 dark:text-purple-300',
    dot: 'bg-purple-500',
  },
  Other: {
    bg: 'bg-slate-500/10',
    border: 'border-slate-500/40',
    text: 'text-slate-700 dark:text-slate-300',
    dot: 'bg-slate-500',
  },
};

export function formatDate(isoDate: string): string {
  const [y, m, d] = isoDate.split('-');
  return `${d}/${m}/${y}`;
}

export function formatTime(iso: string): string {
  return iso.substring(0, 5); // 'HH:mm'
}

/** Returns today's date in 'YYYY-MM-DD' format. */
export function todayIso(): string {
  return new Date().toISOString().substring(0, 10);
}

/** Returns the Monday of the current week in 'YYYY-MM-DD'. */
export function currentWeekMonday(): string {
  const d = new Date();
  const day = d.getDay();
  const diff = d.getDate() - day + (day === 0 ? -6 : 1);
  d.setDate(diff);
  return d.toISOString().substring(0, 10);
}
