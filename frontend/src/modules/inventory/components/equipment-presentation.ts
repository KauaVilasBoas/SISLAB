import type {
  CalibrationStatus,
  EquipmentStatus,
  MaintenanceType,
} from '@/modules/inventory/equipment.types';

/**
 * Presentation helpers for the equipment screen (card [E7] #48): Portuguese labels and the Tailwind
 * badge classes the prototype specifies for the operational status and derived calibration status.
 * The shared Badge primitive only ships 4 neutral variants, so the coloured status pills are built
 * with explicit utility classes here, kept in one place so table and detail stay consistent.
 */

/** Base classes shared by every status pill. */
const PILL =
  'inline-flex items-center gap-1 rounded-full px-2.5 py-0.5 text-xs font-semibold';

/** Portuguese label + coloured pill classes for an equipment's operational status. */
export function equipmentStatusPresentation(status: EquipmentStatus): {
  label: string;
  className: string;
} {
  switch (status) {
    case 'Available':
      return {
        label: 'Disponível',
        className: `${PILL} bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-200`,
      };
    case 'InUse':
      return {
        label: 'Em uso',
        className: `${PILL} bg-blue-100 text-blue-800 dark:bg-blue-950 dark:text-blue-200`,
      };
    case 'UnderMaintenance':
      return {
        label: 'Manutenção',
        className: `${PILL} bg-amber-100 text-amber-800 dark:bg-amber-950 dark:text-amber-200`,
      };
    case 'Inactive':
    default:
      return {
        label: 'Inativo',
        className: `${PILL} bg-muted text-muted-foreground`,
      };
  }
}

/** Portuguese label + coloured pill classes for the derived calibration status. */
export function calibrationStatusPresentation(status: CalibrationStatus): {
  label: string;
  className: string;
} {
  switch (status) {
    case 'Overdue':
      return {
        label: 'Atrasada',
        className: `${PILL} bg-destructive/15 text-destructive`,
      };
    case 'DueSoon':
      return {
        label: 'A vencer',
        className: `${PILL} bg-amber-100 text-amber-800 dark:bg-amber-950 dark:text-amber-200`,
      };
    case 'UpToDate':
      return {
        label: 'Em dia',
        className: `${PILL} bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-200`,
      };
    case 'NotRequired':
    default:
      return {
        label: 'N/A',
        className: `${PILL} bg-muted text-muted-foreground`,
      };
  }
}

/** Portuguese label for a maintenance type. */
export function maintenanceTypeLabel(type: MaintenanceType): string {
  switch (type) {
    case 'Preventive':
      return 'Preventiva';
    case 'Corrective':
      return 'Corretiva';
    case 'Calibration':
      return 'Calibração';
    default:
      return type;
  }
}

/** The operational statuses a user may set from a form, in dropdown order. */
export const EQUIPMENT_STATUSES: EquipmentStatus[] = [
  'Available',
  'InUse',
  'UnderMaintenance',
  'Inactive',
];

/** The calibration statuses the listing filter offers, in dropdown order. */
export const CALIBRATION_STATUSES: CalibrationStatus[] = [
  'Overdue',
  'DueSoon',
  'UpToDate',
  'NotRequired',
];

/** The maintenance types a user may log, in dropdown order. */
export const MAINTENANCE_TYPES: MaintenanceType[] = [
  'Preventive',
  'Corrective',
  'Calibration',
];

/** True when the equipment's calibration should be highlighted as at-risk (overdue). */
export function isCalibrationOverdue(status: CalibrationStatus): boolean {
  return status === 'Overdue';
}

/**
 * Formats an ISO "YYYY-MM-DD" calibration date as the prototype's "MM/AAAA", or a dash when absent.
 * Parsed component-wise (not via Date) so a date-only value never shifts across a timezone boundary.
 */
export function formatCalibrationMonth(isoDate: string | null): string {
  if (!isoDate) return '—';
  const [year, month] = isoDate.split('-');
  if (!year || !month) return '—';
  return `${month}/${year}`;
}
