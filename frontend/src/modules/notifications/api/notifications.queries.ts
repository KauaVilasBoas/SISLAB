import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '@/shared/api/http';
import { Endpoints } from '@/shared/api/endpoints';
import type { PagedResult } from '@/shared/types/api';
import type {
  NotificationListItem,
  UnreadNotificationsCount,
} from '@/modules/notifications/types';

/**
 * Notifications module query keys (card [E7] #65). Namespaced under 'notifications' so the inbox caches its
 * page/filter independently and the mark-as-read mutation can invalidate just this module. `list` is keyed by
 * the unread-only filter and the page, so switching the filter or paging refetches the right slice; `unreadCount`
 * feeds the topbar bell badge and is refetched on a poll (see useUnreadCount).
 */
export const notificationKeys = {
  all: ['notifications'] as const,
  lists: () => [...notificationKeys.all, 'list'] as const,
  list: (unreadOnly: boolean, page: number) =>
    [...notificationKeys.lists(), { unreadOnly, page }] as const,
  unreadCount: () => [...notificationKeys.all, 'unread-count'] as const,
};

/** Page size of the notification inbox — a comfortable, scannable page for the list screen. */
export const NOTIFICATIONS_PAGE_SIZE = 15;

/** How often the bell badge re-polls the unread count (no realtime in this card — refetch/polling only). */
const UNREAD_COUNT_POLL_MS = 45_000;

// ---------------------------------------------------------------------------
// Queries
// ---------------------------------------------------------------------------

/**
 * Paginated list of the active company's notifications, newest first (card #64a read model). When
 * `unreadOnly` is true only unread notifications are returned. Feeds the notifications inbox screen.
 */
export function useNotifications(unreadOnly: boolean, page: number) {
  return useQuery({
    queryKey: notificationKeys.list(unreadOnly, page),
    queryFn: () =>
      api.get<PagedResult<NotificationListItem>>(Endpoints.notifications.list, {
        unreadOnly,
        page,
        pageSize: NOTIFICATIONS_PAGE_SIZE,
      }),
    staleTime: 30_000,
  });
}

/**
 * Unread notification count for the topbar bell badge. Polls on an interval and refetches when the tab
 * regains focus, so the badge stays fresh without websockets (the card explicitly scopes realtime out).
 */
export function useUnreadCount() {
  return useQuery({
    queryKey: notificationKeys.unreadCount(),
    queryFn: () => api.get<UnreadNotificationsCount>(Endpoints.notifications.unreadCount),
    refetchInterval: UNREAD_COUNT_POLL_MS,
    refetchOnWindowFocus: true,
    staleTime: UNREAD_COUNT_POLL_MS,
  });
}

// ---------------------------------------------------------------------------
// Mutation — mark a notification as read (idempotent)
// ---------------------------------------------------------------------------

/**
 * Marks a single notification as read (idempotent on the backend). Optimistic: the read row is flipped
 * across every cached list page and the badge count is decremented immediately, so the UI reacts without
 * waiting for the round-trip. On error the touched caches are rolled back; on settle the whole module is
 * invalidated so the server truth (including the unread-only list dropping the row) is reconciled.
 */
export function useMarkNotificationAsRead() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (notificationId: string) =>
      api.post<void>(Endpoints.notifications.markRead(notificationId)),

    onMutate: async (notificationId: string) => {
      await queryClient.cancelQueries({ queryKey: notificationKeys.all });

      const previousLists = queryClient.getQueriesData<PagedResult<NotificationListItem>>(
        {
          queryKey: notificationKeys.lists(),
        },
      );
      const previousCount = queryClient.getQueryData<UnreadNotificationsCount>(
        notificationKeys.unreadCount(),
      );

      // Flip the row to read in every cached list page (keeps it visible on the "all" list; the
      // unread-only list is reconciled on invalidation at settle).
      let wasUnread = false;
      for (const [key, page] of previousLists) {
        if (!page) continue;
        const target = page.items.find((item) => item.id === notificationId);
        if (target && !target.isRead) wasUnread = true;
        queryClient.setQueryData<PagedResult<NotificationListItem>>(key, {
          ...page,
          items: page.items.map((item) =>
            item.id === notificationId
              ? {
                  ...item,
                  isRead: true,
                  readAtUtc: item.readAtUtc ?? new Date().toISOString(),
                }
              : item,
          ),
        });
      }

      // Only decrement the badge if the row was actually unread (idempotent re-reads must not underflow).
      if (wasUnread && previousCount) {
        queryClient.setQueryData<UnreadNotificationsCount>(
          notificationKeys.unreadCount(),
          {
            unreadCount: Math.max(0, previousCount.unreadCount - 1),
          },
        );
      }

      return { previousLists, previousCount };
    },

    onError: (_error, _notificationId, context) => {
      context?.previousLists.forEach(([key, data]) =>
        queryClient.setQueryData(key, data),
      );
      if (context?.previousCount) {
        queryClient.setQueryData(notificationKeys.unreadCount(), context.previousCount);
      }
    },

    onSettled: () => {
      queryClient.invalidateQueries({ queryKey: notificationKeys.all });
    },
  });
}

// ---------------------------------------------------------------------------
// Mutation — mark ALL as read (idempotent)
// ---------------------------------------------------------------------------

/**
 * Marks every unread notification of the active company as read in one call (card [E7] #65, backend
 * `POST /notifications/read-all`, idempotent). Optimistic and symmetric to {@link useMarkNotificationAsRead}:
 * the badge is zeroed and every cached list row is flipped to read immediately, so the bell and the inbox react
 * before the round-trip. On error the touched caches roll back; on settle the whole module is invalidated so the
 * server truth (including the unread-only list emptying) is reconciled. Returns how many rows the server flipped.
 */
export function useMarkAllAsRead() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: () => api.post<number>(Endpoints.notifications.readAll),

    onMutate: async () => {
      await queryClient.cancelQueries({ queryKey: notificationKeys.all });

      const previousLists = queryClient.getQueriesData<PagedResult<NotificationListItem>>(
        {
          queryKey: notificationKeys.lists(),
        },
      );
      const previousCount = queryClient.getQueryData<UnreadNotificationsCount>(
        notificationKeys.unreadCount(),
      );

      const nowIso = new Date().toISOString();

      // Flip every row to read across all cached list pages (kept visible on the "all" list; the
      // unread-only list is reconciled on invalidation at settle).
      for (const [key, page] of previousLists) {
        if (!page) continue;
        queryClient.setQueryData<PagedResult<NotificationListItem>>(key, {
          ...page,
          items: page.items.map((item) =>
            item.isRead
              ? item
              : { ...item, isRead: true, readAtUtc: item.readAtUtc ?? nowIso },
          ),
        });
      }

      // The whole inbox is acknowledged — the badge goes to zero.
      queryClient.setQueryData<UnreadNotificationsCount>(notificationKeys.unreadCount(), {
        unreadCount: 0,
      });

      return { previousLists, previousCount };
    },

    onError: (_error, _variables, context) => {
      context?.previousLists.forEach(([key, data]) =>
        queryClient.setQueryData(key, data),
      );
      if (context?.previousCount) {
        queryClient.setQueryData(notificationKeys.unreadCount(), context.previousCount);
      }
    },

    onSettled: () => {
      queryClient.invalidateQueries({ queryKey: notificationKeys.all });
    },
  });
}
