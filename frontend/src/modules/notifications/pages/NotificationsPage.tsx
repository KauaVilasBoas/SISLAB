import { useState } from 'react';
import { CheckCheck } from 'lucide-react';
import { PageHeader } from '@/shared/components/PageHeader';
import { Button } from '@/shared/components/ui/button';
import { useToast } from '@/shared/components/ui/toast';
import type { ApiError } from '@/shared/types/api';
import {
  useMarkAllAsRead,
  useMarkNotificationAsRead,
  useNotifications,
  useUnreadCount,
} from '@/modules/notifications/api/notifications.queries';
import { NotificationsList } from '@/modules/notifications/components/NotificationsList';

/**
 * Notifications inbox — the mother screen (card [E7] #65). Owns the filter (all vs. unread-only) and page
 * state, fetches the paginated list and the unread count via TanStack Query, and composes the presentational
 * <NotificationsList>. Marking a row as read is optimistic (the query layer flips the cache and decrements the
 * badge immediately); a failure surfaces as a toast and the cache rolls back. "Marcar todas" acknowledges the
 * whole inbox in one optimistic call (card [E7] #65), disabled while there is nothing unread.
 */
export function NotificationsPage() {
  const toast = useToast();

  const [unreadOnly, setUnreadOnly] = useState(false);
  const [page, setPage] = useState(1);

  const listQuery = useNotifications(unreadOnly, page);
  const unreadCountQuery = useUnreadCount();
  const markAsRead = useMarkNotificationAsRead();
  const markAllAsRead = useMarkAllAsRead();

  const items = listQuery.data?.items ?? [];
  const totalPages = listQuery.data?.totalPages ?? 0;
  const totalCount = listQuery.data?.totalCount ?? 0;
  const unreadCount = unreadCountQuery.data?.unreadCount ?? 0;

  function switchFilter(next: boolean) {
    setUnreadOnly(next);
    setPage(1);
  }

  function handleMarkAsRead(id: string) {
    markAsRead.mutate(id, {
      onError: (error) => {
        toast(
          'error',
          (error as unknown as ApiError)?.message ?? 'Não foi possível marcar como lida.',
        );
      },
    });
  }

  function handleMarkAllAsRead() {
    markAllAsRead.mutate(undefined, {
      onError: (error) => {
        toast(
          'error',
          (error as unknown as ApiError)?.message ??
            'Não foi possível marcar todas como lidas.',
        );
      },
    });
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title="Notificações"
        description="Alertas de validade, estoque baixo, calibração e controlados da sua empresa."
      />

      <div className="flex flex-wrap items-center justify-between gap-3">
        <p className="text-sm text-muted-foreground">
          {unreadCount > 0
            ? `${unreadCount} não ${unreadCount === 1 ? 'lida' : 'lidas'}`
            : 'Nenhuma não lida'}
        </p>
        <div className="flex flex-wrap items-center gap-2">
          <Button
            variant="outline"
            size="sm"
            onClick={handleMarkAllAsRead}
            disabled={unreadCount === 0 || markAllAsRead.isPending}
          >
            <CheckCheck className="size-4" />
            Marcar todas como lidas
          </Button>
          <div className="inline-flex rounded-md border p-0.5">
            <FilterTab active={!unreadOnly} onClick={() => switchFilter(false)}>
              Todas
            </FilterTab>
            <FilterTab active={unreadOnly} onClick={() => switchFilter(true)}>
              Não lidas
            </FilterTab>
          </div>
        </div>
      </div>

      <NotificationsList
        items={items}
        loading={listQuery.isLoading}
        error={listQuery.isError}
        unreadOnly={unreadOnly}
        onMarkAsRead={handleMarkAsRead}
        markingId={markAsRead.isPending ? (markAsRead.variables ?? null) : null}
      />

      <Pagination
        page={page}
        totalPages={totalPages}
        totalCount={totalCount}
        fetching={listQuery.isFetching}
        onPrev={() => setPage((p) => Math.max(1, p - 1))}
        onNext={() => setPage((p) => Math.min(totalPages, p + 1))}
      />
    </div>
  );
}

interface FilterTabProps {
  active: boolean;
  onClick: () => void;
  children: React.ReactNode;
}

/** Segmented control tab for the all/unread-only filter, styled off the shared button tokens. */
function FilterTab({ active, onClick, children }: FilterTabProps) {
  return (
    <button
      type="button"
      onClick={onClick}
      aria-pressed={active}
      className={
        active
          ? 'rounded-[0.3rem] bg-primary px-3 py-1 text-xs font-medium text-primary-foreground'
          : 'rounded-[0.3rem] px-3 py-1 text-xs font-medium text-muted-foreground transition-colors hover:text-foreground'
      }
    >
      {children}
    </button>
  );
}

interface PaginationProps {
  page: number;
  totalPages: number;
  totalCount: number;
  fetching: boolean;
  onPrev: () => void;
  onNext: () => void;
}

/** Prev/next pager, hidden when the list fits on a single page. Mirrors the shared pager used elsewhere. */
function Pagination({
  page,
  totalPages,
  totalCount,
  fetching,
  onPrev,
  onNext,
}: PaginationProps) {
  if (totalPages <= 1) return null;
  return (
    <div className="flex items-center justify-between gap-4 text-sm text-muted-foreground">
      <span>
        {totalCount} {totalCount === 1 ? 'notificação' : 'notificações'} · página {page}{' '}
        de {totalPages}
      </span>
      <div className="flex gap-2">
        <Button
          variant="outline"
          size="sm"
          disabled={page <= 1 || fetching}
          onClick={onPrev}
        >
          Anterior
        </Button>
        <Button
          variant="outline"
          size="sm"
          disabled={page >= totalPages || fetching}
          onClick={onNext}
        >
          Próxima
        </Button>
      </div>
    </div>
  );
}
