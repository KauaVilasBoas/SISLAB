import {
  Bell,
  CalendarClock,
  PackageMinus,
  ShieldAlert,
  Wrench,
  type LucideIcon,
} from 'lucide-react';
import type {
  NotificationSeverity,
  NotificationType,
} from '@/modules/notifications/types';

/**
 * Presentation mapping for the notification inbox (card [E7] #65): type → icon, severity → tone, and the
 * reference → in-app deep-link. Kept out of the components so the pure mapping is reusable and testable and
 * the list stays declarative. Tones reuse the shared `--status-*` tokens (index.css) — no ad-hoc colours.
 */

/** Semantic tone of a notification row, keyed off severity — red for critical, amber for warning, blue for info. */
export type NotificationTone = 'critical' | 'warning' | 'info';

interface SeverityPresentation {
  tone: NotificationTone;
  /** Icon/left-accent colour class, bound to a status token so it tracks the theme. */
  colorClass: string;
  /** Soft tinted surface for the unread accent, matching the tone. */
  accentClass: string;
  label: string;
}

const SEVERITY_PRESENTATION: Record<NotificationSeverity, SeverityPresentation> = {
  Critical: {
    tone: 'critical',
    colorClass: 'text-status-expired',
    accentClass: 'bg-status-expired',
    label: 'Crítico',
  },
  Warning: {
    tone: 'warning',
    colorClass: 'text-status-warning',
    accentClass: 'bg-status-warning',
    label: 'Atenção',
  },
  Info: {
    tone: 'info',
    colorClass: 'text-status-info',
    accentClass: 'bg-status-info',
    label: 'Informativo',
  },
};

/** Icon per notification family, so the operator triages by nature of the risk at a glance. */
const TYPE_ICON: Record<NotificationType, LucideIcon> = {
  Expiry: CalendarClock,
  LowStock: PackageMinus,
  Calibration: Wrench,
  ControlledCompliance: ShieldAlert,
};

/** Short pt-BR label per notification family, for the type chip. */
const TYPE_LABEL: Record<NotificationType, string> = {
  Expiry: 'Validade',
  LowStock: 'Estoque baixo',
  Calibration: 'Calibração',
  ControlledCompliance: 'Controlados',
};

export function severityPresentation(
  severity: NotificationSeverity,
): SeverityPresentation {
  return SEVERITY_PRESENTATION[severity] ?? SEVERITY_PRESENTATION.Info;
}

export function notificationIcon(type: NotificationType): LucideIcon {
  return TYPE_ICON[type] ?? Bell;
}

export function notificationTypeLabel(type: NotificationType): string {
  return TYPE_LABEL[type] ?? 'Notificação';
}

/**
 * Resolves the in-app route a notification deep-links to, from its reference target type. The reference id is
 * carried by value (never a cross-module entity), so the deep-link is a best-effort navigation to the owning
 * resource screen. Unknown target types have no deep-link (returns null → the row is not a link).
 *
 * TODO: when the resource screens gain per-id detail routes (e.g. /inventory/:id, /equipment/:id) route to the
 * specific record using `targetId` instead of just the list screen.
 */
export function notificationDeepLink(targetType: string): string | null {
  switch (targetType) {
    case 'stock_item':
      return '/inventory';
    case 'equipment':
      return '/equipment';
    default:
      return null;
  }
}
