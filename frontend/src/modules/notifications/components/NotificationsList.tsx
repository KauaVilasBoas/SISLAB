import { useNavigate } from 'react-router-dom';
import { BellOff, Check, ChevronRight, Loader2 } from 'lucide-react';
import { Badge } from '@/shared/components/ui/badge';
import { Button } from '@/shared/components/ui/button';
import { Card, CardContent } from '@/shared/components/ui/card';
import { cn } from '@/shared/lib/utils';
import { formatRelativeTime } from '@/shared/lib/format';
import type { NotificationListItem } from '@/modules/notifications/types';
import {
  notificationDeepLink,
  notificationIcon,
  notificationTypeLabel,
  severityPresentation,
} from '@/modules/notifications/components/notification-presentation';

interface NotificationsListProps {
  items: NotificationListItem[];
  loading: boolean;
  error: boolean;
  /** Whether the "só não-lidas" filter is on — tailors the empty-state copy. */
  unreadOnly: boolean;
  /** Marks one notification as read (optimistic upstream); the id currently being marked drives the spinner. */
  onMarkAsRead: (id: string) => void;
  markingId: string | null;
}

/** How many skeleton rows the loading state renders — a full-ish page so the layout does not jump. */
const SKELETON_ROWS = 6;

/**
 * Presentational notification inbox list (card [E7] #65). Renders each notification with its type icon and
 * chip, title/description, relative timestamp, a read/unread accent, a deep-link into the referenced resource
 * and a "marcar como lida" action for unread rows. Pure — loading (skeleton), error and empty are the
 * standardized card states; all data and the mark-as-read handler come from the mother screen.
 */
export function NotificationsList({
  items,
  loading,
  error,
  unreadOnly,
  onMarkAsRead,
  markingId,
}: NotificationsListProps) {
  if (loading) {
    return (
      <ul className="space-y-2" aria-busy="true" aria-label="Carregando notificações">
        {Array.from({ length: SKELETON_ROWS }).map((_, i) => (
          <li key={i} className="h-[4.5rem] animate-pulse rounded-xl border bg-muted" />
        ))}
      </ul>
    );
  }

  if (error) {
    return (
      <StateCard>
        <BellOff className="size-8 text-muted-foreground" />
        <p className="text-sm text-muted-foreground">
          Não foi possível carregar as notificações.
        </p>
      </StateCard>
    );
  }

  if (items.length === 0) {
    return (
      <StateCard>
        <BellOff className="size-8 text-muted-foreground" />
        <p className="text-sm text-muted-foreground">
          {unreadOnly
            ? 'Nenhuma notificação não lida. Tudo em dia por aqui.'
            : 'Nenhuma notificação. Alertas de validade, estoque, calibração e controlados aparecem aqui.'}
        </p>
      </StateCard>
    );
  }

  return (
    <ul className="space-y-2">
      {items.map((item) => (
        <NotificationRow
          key={item.id}
          item={item}
          onMarkAsRead={onMarkAsRead}
          marking={markingId === item.id}
        />
      ))}
    </ul>
  );
}

interface NotificationRowProps {
  item: NotificationListItem;
  onMarkAsRead: (id: string) => void;
  marking: boolean;
}

function NotificationRow({ item, onMarkAsRead, marking }: NotificationRowProps) {
  const navigate = useNavigate();
  const severity = severityPresentation(item.severity);
  const Icon = notificationIcon(item.type);
  const deepLink = notificationDeepLink(item.referenceTargetType);

  function goToResource() {
    if (deepLink) navigate(deepLink);
  }

  return (
    <li>
      <Card
        className={cn(
          'relative overflow-hidden transition-colors',
          deepLink && 'cursor-pointer hover:bg-accent',
          !item.isRead && 'bg-accent/40',
        )}
        onClick={deepLink ? goToResource : undefined}
      >
        {/* Left accent bar — coloured by severity, shown only while unread. */}
        {!item.isRead ? (
          <span
            aria-hidden
            className={cn('absolute inset-y-0 left-0 w-1', severity.accentClass)}
          />
        ) : null}

        <CardContent className="flex items-start gap-3 p-4 pl-5">
          <Icon className={cn('mt-0.5 size-5 shrink-0', severity.colorClass)} />

          <div className="min-w-0 flex-1">
            <div className="flex flex-wrap items-center gap-2">
              <p
                className={cn(
                  'text-sm',
                  item.isRead ? 'font-medium text-foreground' : 'font-semibold',
                )}
              >
                {item.title}
              </p>
              <Badge variant="muted" className="shrink-0">
                {notificationTypeLabel(item.type)}
              </Badge>
              {!item.isRead ? (
                <span
                  className={cn(
                    'inline-block size-2 shrink-0 rounded-full',
                    severity.accentClass,
                  )}
                  aria-label="Não lida"
                  title="Não lida"
                />
              ) : null}
            </div>
            <p className="mt-1 text-sm text-muted-foreground">{item.description}</p>
            <p className="mt-1 text-xs text-muted-foreground">
              {formatRelativeTime(item.createdAtUtc)}
            </p>
          </div>

          <div className="flex shrink-0 items-center gap-1">
            {!item.isRead ? (
              <Button
                variant="ghost"
                size="sm"
                title="Marcar como lida"
                disabled={marking}
                onClick={(e) => {
                  e.stopPropagation();
                  onMarkAsRead(item.id);
                }}
              >
                {marking ? (
                  <Loader2 className="size-4 animate-spin" />
                ) : (
                  <Check className="size-4" />
                )}
                <span className="hidden sm:inline">Marcar como lida</span>
              </Button>
            ) : null}
            {deepLink ? (
              <ChevronRight className="size-4 shrink-0 text-muted-foreground" />
            ) : null}
          </div>
        </CardContent>
      </Card>
    </li>
  );
}

function StateCard({ children }: { children: React.ReactNode }) {
  return (
    <Card>
      <CardContent className="flex flex-col items-center justify-center gap-3 py-16 text-center">
        {children}
      </CardContent>
    </Card>
  );
}
