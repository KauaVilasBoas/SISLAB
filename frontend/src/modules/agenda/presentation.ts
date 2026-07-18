import type { AgendaActivity, AssignmentStatus, PresentationStatus, PresentationType, RoomType } from './types';

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
