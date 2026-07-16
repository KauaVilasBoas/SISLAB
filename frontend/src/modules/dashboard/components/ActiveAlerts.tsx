import { Link } from 'react-router-dom';
import {
  AlertTriangle,
  CalendarX2,
  ChevronRight,
  PackageMinus,
  Wrench,
  type LucideIcon,
} from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '@/shared/components/ui/card';
import { cn } from '@/shared/lib/utils';
import { formatNumber } from '@/shared/lib/format';
import type { EquipmentListItem } from '@/modules/inventory/equipment.types';
import type { BelowMinimumItem, ExpiringItem } from '@/modules/dashboard/types';

/** Severity tone of an alert — red for already-broken invariants, amber for imminent risk. */
type AlertTone = 'critical' | 'warning';

interface AlertEntry {
  id: string;
  icon: LucideIcon;
  tone: AlertTone;
  title: string;
  description: string;
  /** Route the chevron navigates to (the relevant resource screen). */
  to: string;
  /** Sort weight — lower comes first (most urgent on top). */
  order: number;
}

interface ActiveAlertsProps {
  expiring?: ExpiringItem[];
  belowMinimum?: BelowMinimumItem[];
  overdueCalibration?: EquipmentListItem[];
  loading?: boolean;
}

const TONE_CLASSES: Record<AlertTone, string> = {
  critical: 'text-status-expired',
  warning: 'text-status-warning',
};

/** Maps an expiring/expired item to an alert; expired is critical, expiring-soon is a warning. */
function expiringToAlert(item: ExpiringItem): AlertEntry {
  const expired = item.daysRemaining < 0;
  const controlled = item.isControlled ? ' (controlado)' : '';
  const location = item.storageLocationName ?? 'local não informado';
  return {
    id: `expiry-${item.id}`,
    icon: CalendarX2,
    tone: expired ? 'critical' : 'warning',
    title: expired ? `Vencido: ${item.name}${controlled}` : `A vencer: ${item.name}${controlled}`,
    description: expired
      ? `Vencido há ${formatNumber(Math.abs(item.daysRemaining))} dia(s) · ${location}`
      : `Vence em ${formatNumber(item.daysRemaining)} dia(s) · ${location}`,
    to: '/inventory',
    order: expired ? 0 : 2,
  };
}

/** Maps a below-minimum item to a warning alert. */
function belowMinimumToAlert(item: BelowMinimumItem): AlertEntry {
  const location = item.storageLocationName ?? 'local não informado';
  return {
    id: `low-${item.id}`,
    icon: PackageMinus,
    tone: 'warning',
    title: `Abaixo do mínimo: ${item.name}`,
    description: `${formatNumber(item.quantity)} ${item.unit} · mínimo ${formatNumber(
      item.minimumQuantity,
    )} ${item.minimumUnit} · ${location}`,
    to: '/inventory',
    order: 3,
  };
}

/** Maps an overdue-calibration equipment to a critical alert. */
function calibrationToAlert(item: EquipmentListItem): AlertEntry {
  return {
    id: `calib-${item.id}`,
    icon: Wrench,
    tone: 'critical',
    title: `Calibração vencida: ${item.name}`,
    description: `Patrimônio ${item.assetTag}${
      item.storageLocationName ? ` · ${item.storageLocationName}` : ''
    }`,
    to: '/equipment',
    order: 1,
  };
}

/** How many alerts the widget shows at most — a short, prioritized list, not an inbox. */
const MAX_ALERTS = 8;

function composeAlerts({
  expiring = [],
  belowMinimum = [],
  overdueCalibration = [],
}: ActiveAlertsProps): AlertEntry[] {
  return [
    ...expiring.map(expiringToAlert),
    ...belowMinimum.map(belowMinimumToAlert),
    ...overdueCalibration.map(calibrationToAlert),
  ]
    .sort((a, b) => a.order - b.order)
    .slice(0, MAX_ALERTS);
}

/**
 * Presentational "Alertas ativos" list: composes the at-risk items, low-stock items and overdue
 * calibrations fetched by the mother screen into one prioritized, tone-coded list (critical first).
 * Each row has an icon, title, description and a chevron into the relevant resource screen. Fed by the
 * same read-side queries the E6 alert jobs (#41/#42/calibração) key off.
 */
export function ActiveAlerts(props: ActiveAlertsProps) {
  const { loading } = props;
  const alerts = composeAlerts(props);

  return (
    <Card>
      <CardHeader className="flex flex-row items-center gap-2 space-y-0">
        <AlertTriangle className="size-4 text-status-warning" />
        <CardTitle>Alertas ativos</CardTitle>
      </CardHeader>
      <CardContent>
        {loading ? (
          <ul className="space-y-2">
            {Array.from({ length: 3 }).map((_, i) => (
              <li key={i} className="h-14 animate-pulse rounded-lg bg-muted" />
            ))}
          </ul>
        ) : alerts.length === 0 ? (
          <p className="py-8 text-center text-sm text-muted-foreground">
            Nenhum alerta ativo. Estoque, validade e calibração em dia.
          </p>
        ) : (
          <ul className="space-y-2">
            {alerts.map((alert) => (
              <li key={alert.id}>
                <Link
                  to={alert.to}
                  className="flex items-center gap-3 rounded-lg border p-3 transition-colors hover:bg-accent"
                >
                  <alert.icon className={cn('size-5 shrink-0', TONE_CLASSES[alert.tone])} />
                  <div className="min-w-0 flex-1">
                    <p className="truncate text-sm font-medium">{alert.title}</p>
                    <p className="truncate text-xs text-muted-foreground">{alert.description}</p>
                  </div>
                  <ChevronRight className="size-4 shrink-0 text-muted-foreground" />
                </Link>
              </li>
            ))}
          </ul>
        )}
      </CardContent>
    </Card>
  );
}
