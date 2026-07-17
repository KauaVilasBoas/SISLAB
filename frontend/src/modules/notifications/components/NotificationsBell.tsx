import { useNavigate } from 'react-router-dom';
import { Bell, BellOff, Check, CheckCheck, Loader2 } from 'lucide-react';
import { Button } from '@/shared/components/ui/button';
import { Popover } from '@/shared/components/ui/popover';
import { RequirePermission } from '@/modules/auth/PermissionsProvider';
import { Permissions } from '@/modules/auth/permissions';
import { cn } from '@/shared/lib/utils';
import { formatRelativeTime } from '@/shared/lib/format';
import type { ApiError } from '@/shared/types/api';
import { useToast } from '@/shared/components/ui/toast';
import type { NotificationListItem } from '@/modules/notifications/types';
import {
  useMarkAllAsRead,
  useMarkNotificationAsRead,
  useNotifications,
  useUnreadCount,
} from '@/modules/notifications/api/notifications.queries';
import {
  notificationDeepLink,
  notificationIcon,
  notificationTypeLabel,
  severityPresentation,
} from '@/modules/notifications/components/notification-presentation';

/** How many recent notifications the dropdown shows — a short peek, not the whole inbox (that is /notifications). */
const DROPDOWN_PAGE_SIZE = 8;

/**
 * Notification center in the topbar bell (card [E7] #65). Clicking the bell opens a dropdown/panel with the most
 * recent notifications (icon per type, title/description, severity tone, relative timestamp), a per-item
 * "marcar como lida", a "marcar todas", and a footer link to the full inbox. It reuses the shared presentation
 * mapping (icon/tone/deep-link) so nothing is duplicated; the page at /notifications remains the "Ver todas"
 * destination. Deep-linking a row navigates to the owning resource and closes the panel.
 */
export function NotificationsBell() {
  const { data: unread } = useUnreadCount();
  const unreadCount = unread?.unreadCount ?? 0;
  const badgeLabel = unreadCount > 9 ? '9+' : String(unreadCount);

  return (
    <Popover
      label="Central de notificações"
      className="w-[22rem] max-w-[calc(100vw-1.5rem)]"
      trigger={
        <Button
          variant="ghost"
          size="icon"
          className="relative"
          aria-label={
            unreadCount > 0 ? `Notificações (${unreadCount} não lidas)` : 'Notificações'
          }
          title="Notificações"
        >
          <Bell className="size-5" />
          {unreadCount > 0 ? (
            <span className="absolute -right-0.5 -top-0.5 flex h-4 min-w-4 items-center justify-center rounded-full bg-status-expired px-1 text-[10px] font-semibold leading-none text-status-foreground">
              {badgeLabel}
            </span>
          ) : null}
        </Button>
      }
    >
      {(close) => <NotificationsPanel unreadCount={unreadCount} onClose={close} />}
    </Popover>
  );
}

interface NotificationsPanelProps {
  unreadCount: number;
  onClose: () => void;
}

function NotificationsPanel({ unreadCount, onClose }: NotificationsPanelProps) {
  const navigate = useNavigate();
  const toast = useToast();

  // Recent notifications (all, newest first) — the panel peeks the top of the inbox, not just unread ones.
  const listQuery = useNotifications(false, 1);
  const markAsRead = useMarkNotificationAsRead();
  const markAllAsRead = useMarkAllAsRead();

  const items = (listQuery.data?.items ?? []).slice(0, DROPDOWN_PAGE_SIZE);

  function handleMarkAsRead(id: string) {
    markAsRead.mutate(id, {
      onError: (error) =>
        toast(
          'error',
          (error as unknown as ApiError)?.message ?? 'Não foi possível marcar como lida.',
        ),
    });
  }

  function handleMarkAllAsRead() {
    markAllAsRead.mutate(undefined, {
      onError: (error) =>
        toast(
          'error',
          (error as unknown as ApiError)?.message ??
            'Não foi possível marcar todas como lidas.',
        ),
    });
  }

  function seeAll() {
    onClose();
    navigate('/notifications');
  }

  return (
    <div className="flex max-h-[70vh] flex-col">
      <header className="flex items-center justify-between gap-2 border-b px-4 py-3">
        <div className="flex items-center gap-2">
          <span className="text-sm font-semibold">Notificações</span>
          {unreadCount > 0 ? (
            <span className="text-xs text-muted-foreground">
              {unreadCount} não {unreadCount === 1 ? 'lida' : 'lidas'}
            </span>
          ) : null}
        </div>
        <RequirePermission code={Permissions.notifications.readAll}>
          <Button
            variant="ghost"
            size="sm"
            className="h-7 px-2 text-xs"
            onClick={handleMarkAllAsRead}
            disabled={unreadCount === 0 || markAllAsRead.isPending}
            title="Marcar todas como lidas"
          >
            <CheckCheck className="size-4" />
            <span className="hidden sm:inline">Marcar todas</span>
          </Button>
        </RequirePermission>
      </header>

      <div className="min-h-0 flex-1 overflow-y-auto">
        <PanelBody
          items={items}
          loading={listQuery.isLoading}
          error={listQuery.isError}
          onMarkAsRead={handleMarkAsRead}
          markingId={markAsRead.isPending ? (markAsRead.variables ?? null) : null}
          onClose={onClose}
        />
      </div>

      <footer className="border-t p-2">
        <Button
          variant="ghost"
          size="sm"
          className="w-full justify-center"
          onClick={seeAll}
        >
          Ver todas
        </Button>
      </footer>
    </div>
  );
}

interface PanelBodyProps {
  items: NotificationListItem[];
  loading: boolean;
  error: boolean;
  onMarkAsRead: (id: string) => void;
  markingId: string | null;
  onClose: () => void;
}

function PanelBody({
  items,
  loading,
  error,
  onMarkAsRead,
  markingId,
  onClose,
}: PanelBodyProps) {
  if (loading) {
    return (
      <ul className="space-y-1 p-2" aria-busy="true" aria-label="Carregando notificações">
        {Array.from({ length: 4 }).map((_, i) => (
          <li key={i} className="h-14 animate-pulse rounded-lg bg-muted" />
        ))}
      </ul>
    );
  }

  if (error) {
    return <PanelState>Não foi possível carregar as notificações.</PanelState>;
  }

  if (items.length === 0) {
    return <PanelState>Nenhuma notificação. Tudo em dia por aqui.</PanelState>;
  }

  return (
    <ul className="divide-y">
      {items.map((item) => (
        <NotificationDropdownRow
          key={item.id}
          item={item}
          onMarkAsRead={onMarkAsRead}
          marking={markingId === item.id}
          onClose={onClose}
        />
      ))}
    </ul>
  );
}

interface NotificationDropdownRowProps {
  item: NotificationListItem;
  onMarkAsRead: (id: string) => void;
  marking: boolean;
  onClose: () => void;
}

/**
 * Compact notification row for the dropdown. Reuses the shared presentation mapping (icon/tone/deep-link) so the
 * bell and the full inbox stay visually consistent without duplicating that logic. Clicking the row deep-links
 * to the owning resource (when known) and closes the panel; the check button acknowledges just this one.
 */
function NotificationDropdownRow({
  item,
  onMarkAsRead,
  marking,
  onClose,
}: NotificationDropdownRowProps) {
  const navigate = useNavigate();
  const severity = severityPresentation(item.severity);
  const Icon = notificationIcon(item.type);
  const deepLink = notificationDeepLink(item.referenceTargetType);

  function goToResource() {
    if (!deepLink) return;
    onClose();
    navigate(deepLink);
  }

  return (
    <li
      className={cn(
        'flex items-start gap-3 px-4 py-3 transition-colors',
        deepLink && 'cursor-pointer hover:bg-accent',
        !item.isRead && 'bg-accent/40',
      )}
      onClick={deepLink ? goToResource : undefined}
    >
      <Icon className={cn('mt-0.5 size-4 shrink-0', severity.colorClass)} />

      <div className="min-w-0 flex-1">
        <div className="flex items-center gap-2">
          <p
            className={cn(
              'truncate text-sm',
              item.isRead ? 'font-medium' : 'font-semibold',
            )}
          >
            {item.title}
          </p>
          {!item.isRead ? (
            <span
              className={cn('size-2 shrink-0 rounded-full', severity.accentClass)}
              aria-label="Não lida"
              title="Não lida"
            />
          ) : null}
        </div>
        <p className="mt-0.5 line-clamp-2 text-xs text-muted-foreground">
          {item.description}
        </p>
        <p className="mt-0.5 text-[11px] text-muted-foreground">
          {notificationTypeLabel(item.type)} · {formatRelativeTime(item.createdAtUtc)}
        </p>
      </div>

      {!item.isRead ? (
        <RequirePermission code={Permissions.notifications.markAsRead}>
          <Button
            variant="ghost"
            size="icon"
            className="size-7 shrink-0"
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
          </Button>
        </RequirePermission>
      ) : null}
    </li>
  );
}

function PanelState({ children }: { children: React.ReactNode }) {
  return (
    <div className="flex flex-col items-center justify-center gap-2 px-4 py-10 text-center">
      <BellOff className="size-6 text-muted-foreground" />
      <p className="text-sm text-muted-foreground">{children}</p>
    </div>
  );
}
