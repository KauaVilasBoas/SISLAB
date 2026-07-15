/**
 * Dashboard read-models — mirror the Inventory read-side DTOs
 * (GetConsumptionSeriesQuery, GetExpirySummaryQuery, GetBelowMinimumSummaryQuery),
 * serialized as camelCase JSON.
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
