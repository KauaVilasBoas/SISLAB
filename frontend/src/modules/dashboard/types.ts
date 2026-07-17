/**
 * Dashboard read-models — mirror the Inventory read-side DTOs
 * (GetConsumptionSeriesQuery, GetExpirySummaryQuery, GetBelowMinimumSummaryQuery,
 * ListExpiringItemsQuery, ListItemsBelowMinimumQuery), serialized as camelCase JSON.
 */
export type ConsumptionBucket = 'Day' | 'Month';

export interface ConsumptionSeriesPoint {
  /** Bucket start as an ISO date (yyyy-MM-dd). */
  bucketStart: string;
  unit: string;
  totalConsumed: number;
}

export interface ConsumptionPeriodTotal {
  unit: string;
  currentTotal: number;
  previousTotal: number;
  /** Signed % vs. the preceding period; null when there's no baseline ("novo"). */
  deltaPercentage: number | null;
}

export interface ConsumptionSeries {
  bucket: ConsumptionBucket;
  points: ConsumptionSeriesPoint[];
  totals: ConsumptionPeriodTotal[];
}

/** Donut totals over items that carry a validity. */
export interface ExpirySummary {
  expired: number;
  expiringSoon: number;
  ok: number;
  total: number;
}

export interface BelowMinimumSummary {
  belowMinimumCount: number;
}

/** Derived expiry classification, mirroring the backend ExpiryStatusView enum. */
export type ExpiryStatusView = 'NotApplicable' | 'Ok' | 'ExpiringSoon' | 'Expired';

/**
 * An at-risk stock item row (GET /stock-items/expiring). Feeds the active-alerts list; carries the
 * `isControlled` flag so the dashboard can surface how many expired items are controlled drugs.
 */
export interface ExpiringItem {
  id: string;
  name: string;
  category: string;
  lotCode: string | null;
  quantity: number;
  unit: string;
  expiryYear: number;
  expiryMonth: number;
  expiryStatus: ExpiryStatusView;
  /** Signed days to the last valid day; negative once expired. */
  daysRemaining: number;
  isControlled: boolean;
  storageLocationId: string;
  storageLocationName: string | null;
  storageLocationType: string | null;
}

/** A below-minimum stock item row (GET /stock-items/below-minimum). Feeds the active-alerts list. */
export interface BelowMinimumItem {
  id: string;
  name: string;
  category: string;
  brand: string | null;
  quantity: number;
  unit: string;
  minimumQuantity: number;
  minimumUnit: string;
  deficit: number;
  isControlled: boolean;
  storageLocationId: string;
  storageLocationName: string | null;
  storageLocationType: string | null;
}
