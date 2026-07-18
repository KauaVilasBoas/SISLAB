/**
 * UI contracts for the notifications module (card [E7] #65), mirroring the backend read models exposed by
 * the NotificationsController (card #64a). The list item is the flat read shape the bell/inbox renders — it
 * carries the deep-link target as `referenceTargetType` + `referenceTargetId`, never a cross-module entity.
 */

/**
 * Family of condition a notification reports. String literals, because the backend persists the domain enum
 * with `HasConversion<string>()` — the wire value is the enum name, not a number.
 */
export type NotificationType =
  | 'Expiry'
  | 'LowStock'
  | 'Calibration'
  | 'ControlledCompliance'
  | 'PresentationReminder'
  | 'BioteriumReminder';

/** How urgently the operator should act — drives the tone (red/amber/blue) of the row and the type icon. */
export type NotificationSeverity = 'Info' | 'Warning' | 'Critical';

/** One row of the company's notification inbox. Matches `NotificationListItem` on the backend field-for-field. */
export interface NotificationListItem {
  id: string;
  type: NotificationType;
  severity: NotificationSeverity;
  title: string;
  description: string;
  /** Target kind slug for the deep-link (e.g. `stock_item`, `equipment`). */
  referenceTargetType: string;
  /** Target id (by value) for the deep-link — never a live cross-module reference. */
  referenceTargetId: string;
  isRead: boolean;
  createdAtUtc: string;
  readAtUtc: string | null;
}

/** The single number the bell badge shows: how many notifications are currently unread for the active company. */
export interface UnreadNotificationsCount {
  unreadCount: number;
}
