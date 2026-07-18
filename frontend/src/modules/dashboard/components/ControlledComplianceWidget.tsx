import { Link } from 'react-router-dom';
import { ChevronRight, ShieldAlert } from 'lucide-react';
import { Badge } from '@/shared/components/ui/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/shared/components/ui/card';
import { cn } from '@/shared/lib/utils';
import { useNotifications } from '@/modules/notifications/api/notifications.queries';
import type { NotificationListItem } from '@/modules/notifications/types';

const SEVERITY_CLASS: Record<string, string> = {
  Critical: 'text-status-expired',
  Warning: 'text-status-warning',
  Info: 'text-muted-foreground',
};

function ComplianceRow({ item }: { item: NotificationListItem }) {
  return (
    <li>
      <Link
        to="/notifications"
        className="flex items-center gap-3 rounded-lg border p-3 transition-colors hover:bg-accent"
      >
        <ShieldAlert
          className={cn('size-5 shrink-0', SEVERITY_CLASS[item.severity] ?? 'text-muted-foreground')}
        />
        <div className="min-w-0 flex-1">
          <p className={cn('truncate text-sm font-medium', !item.isRead && 'font-semibold')}>
            {item.title}
          </p>
          <p className="truncate text-xs text-muted-foreground">{item.description}</p>
        </div>
        <ChevronRight className="size-4 shrink-0 text-muted-foreground" />
      </Link>
    </li>
  );
}

const MAX_ITEMS = 5;

/**
 * Dashboard complement for card [E7] #108: shows the most-recent ControlledCompliance notifications
 * produced by the daily compliance-alert job. Renders only when at least one such notification exists
 * (the widget self-hides when clean). This is the complementary section approach described in #108 —
 * it does NOT replace the live `ActiveAlerts` widget; it adds a durable, notification-API-backed view
 * of past compliance events alongside the real-time inventory state.
 */
export function ControlledComplianceWidget() {
  // We load the first page (all-notifications, unread + read) and filter client-side.
  // The dataset is small (labs don't have thousands of notifications) so one-page filtering is fine.
  const { data, isLoading } = useNotifications(false, 1);

  const complianceItems = (data?.items ?? [])
    .filter((n) => n.type === 'ControlledCompliance')
    .slice(0, MAX_ITEMS);

  const unreadCount = complianceItems.filter((n) => !n.isRead).length;

  // Self-hide while loading or when there are no compliance notifications — no point showing an empty card.
  if (isLoading || complianceItems.length === 0) return null;

  return (
    <Card>
      <CardHeader className="flex flex-row items-center gap-2 space-y-0">
        <ShieldAlert className="size-4 text-status-expired" />
        <CardTitle className="flex-1">Controlados — conformidade</CardTitle>
        {unreadCount > 0 && (
          <Badge variant="default">{unreadCount}</Badge>
        )}
      </CardHeader>
      <CardContent>
        <ul className="space-y-2">
          {complianceItems.map((item) => (
            <ComplianceRow key={item.id} item={item} />
          ))}
        </ul>
        <div className="mt-3 text-right">
          <Link
            to="/notifications"
            className="text-sm text-muted-foreground underline-offset-4 hover:text-foreground hover:underline"
          >
            Ver todas as notificações →
          </Link>
        </div>
      </CardContent>
    </Card>
  );
}
