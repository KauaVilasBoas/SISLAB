import type { PagedResult } from '@/shared/types/api';
import type {
  BelowMinimumItem,
  BelowMinimumSummary,
  ConsumptionSeries,
  ExpiringItem,
  ExpirySummary,
} from '@/modules/dashboard/types';
import type {
  LocationSummaryItem,
  RecentMovementItem,
  StockItemListItem,
} from '@/modules/inventory/types';
import type {
  NotificationListItem,
  UnreadNotificationsCount,
} from '@/modules/notifications/types';
import type { PendenciesResult } from '@/modules/in-vivo/types';
import type { CalendarItem } from '@/modules/agenda/types';

/**
 * Seeded, fictional read-side data for the backend-less demo (hero screens: dashboard, inventory, agenda).
 * All names/lots/people are invented — nothing from the real pilot lab (LAFTE). Dates are computed relative
 * to "now" at load time, so the calendar always lands events in the current view.
 */

// --- date helpers -----------------------------------------------------------
const now = new Date();
const pad = (n: number) => String(n).padStart(2, '0');
const isoDate = (d: Date) =>
  `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
function addDays(base: Date, days: number): Date {
  const d = new Date(base);
  d.setDate(d.getDate() + days);
  return d;
}

/** Wrap a list as a single-page PagedResult. */
export function paged<T>(items: T[]): PagedResult<T> {
  return {
    items,
    page: 1,
    pageSize: Math.max(items.length, 20),
    totalCount: items.length,
    totalPages: 1,
  };
}

// --- Inventory --------------------------------------------------------------
export const DEMO_STOCK_ITEMS: StockItemListItem[] = [
  {
    id: 'si-1',
    name: 'Cetamina 10%',
    category: 'Anestésico',
    brand: 'Syntec',
    lotCode: 'CET-2405',
    quantity: 3,
    unit: 'frasco',
    minimumQuantity: 5,
    minimumUnit: 'frasco',
    isBelowMinimum: true,
    expiryYear: 2026,
    expiryMonth: 11,
    expiryStatus: 'ExpiringSoon',
    containerState: 'Closed',
    isControlled: true,
    storageLocationId: 'loc-3',
    storageLocationName: 'Armário de Controlados',
    storageLocationType: 'Controlled',
    application: 'Anestesia cirúrgica',
  },
  {
    id: 'si-2',
    name: 'Xilazina 2%',
    category: 'Sedativo',
    brand: 'Ceva',
    lotCode: 'XIL-2312',
    quantity: 6,
    unit: 'frasco',
    minimumQuantity: 4,
    minimumUnit: 'frasco',
    isBelowMinimum: false,
    expiryYear: 2027,
    expiryMonth: 3,
    expiryStatus: 'Ok',
    containerState: 'Open',
    isControlled: true,
    storageLocationId: 'loc-3',
    storageLocationName: 'Armário de Controlados',
    storageLocationType: 'Controlled',
    application: 'Sedação associada',
  },
  {
    id: 'si-3',
    name: 'Formaldeído 37%',
    category: 'Fixador',
    brand: 'Sigma-Aldrich',
    lotCode: 'FOR-2401',
    quantity: 2500,
    unit: 'mL',
    minimumQuantity: 1000,
    minimumUnit: 'mL',
    isBelowMinimum: false,
    expiryYear: 2028,
    expiryMonth: 1,
    expiryStatus: 'Ok',
    containerState: 'Closed',
    isControlled: false,
    storageLocationId: 'loc-2',
    storageLocationName: 'Armário de Reagentes A',
    storageLocationType: 'ReagentCabinet',
    application: 'Fixação de tecidos',
  },
  {
    id: 'si-4',
    name: 'DMSO',
    category: 'Solvente',
    brand: 'Sigma-Aldrich',
    lotCode: 'DMS-2404',
    quantity: 120,
    unit: 'mL',
    minimumQuantity: 100,
    minimumUnit: 'mL',
    isBelowMinimum: false,
    expiryYear: 2027,
    expiryMonth: 9,
    expiryStatus: 'Ok',
    containerState: 'Open',
    isControlled: false,
    storageLocationId: 'loc-2',
    storageLocationName: 'Armário de Reagentes A',
    storageLocationType: 'ReagentCabinet',
    application: 'Diluição de compostos',
  },
  {
    id: 'si-5',
    name: 'Kit Griess (Nitrito)',
    category: 'Kit de ensaio',
    brand: 'Promega',
    lotCode: 'GRI-2403',
    quantity: 1,
    unit: 'kit',
    minimumQuantity: 1,
    minimumUnit: 'kit',
    isBelowMinimum: false,
    expiryYear: 2026,
    expiryMonth: 9,
    expiryStatus: 'ExpiringSoon',
    containerState: 'Closed',
    isControlled: false,
    storageLocationId: 'loc-1',
    storageLocationName: 'Geladeira 4°C — Sala 2',
    storageLocationType: 'Refrigerated',
    application: 'Dosagem de óxido nítrico',
  },
  {
    id: 'si-6',
    name: 'Soro Fisiológico 0,9%',
    category: 'Solução',
    brand: 'Fresenius',
    lotCode: 'SF-2406',
    quantity: 40,
    unit: 'frasco',
    minimumQuantity: 20,
    minimumUnit: 'frasco',
    isBelowMinimum: false,
    expiryYear: 2027,
    expiryMonth: 6,
    expiryStatus: 'Ok',
    containerState: 'Closed',
    isControlled: false,
    storageLocationId: 'loc-4',
    storageLocationName: 'Estoque Geral',
    storageLocationType: 'GeneralStorage',
    application: 'Veículo / lavagem',
  },
  {
    id: 'si-7',
    name: 'Heparina Sódica',
    category: 'Anticoagulante',
    brand: 'Blau',
    lotCode: 'HEP-2402',
    quantity: 2,
    unit: 'frasco',
    minimumQuantity: 6,
    minimumUnit: 'frasco',
    isBelowMinimum: true,
    expiryYear: 2026,
    expiryMonth: 12,
    expiryStatus: 'ExpiringSoon',
    containerState: 'Open',
    isControlled: false,
    storageLocationId: 'loc-1',
    storageLocationName: 'Geladeira 4°C — Sala 2',
    storageLocationType: 'Refrigerated',
    application: 'Coleta de sangue',
  },
  {
    id: 'si-8',
    name: 'Ração para roedores',
    category: 'Insumo biotério',
    brand: 'Nuvilab',
    lotCode: 'RAC-2405',
    quantity: 8,
    unit: 'kg',
    minimumQuantity: 15,
    minimumUnit: 'kg',
    isBelowMinimum: true,
    expiryYear: 2027,
    expiryMonth: 2,
    expiryStatus: 'Ok',
    containerState: 'Open',
    isControlled: false,
    storageLocationId: 'loc-4',
    storageLocationName: 'Estoque Geral',
    storageLocationType: 'GeneralStorage',
    application: 'Manutenção dos animais',
  },
];

export const DEMO_LOCATIONS: LocationSummaryItem[] = [
  {
    id: 'loc-1',
    name: 'Geladeira 4°C — Sala 2',
    type: 'Refrigerated',
    isActive: true,
    itemCount: 24,
    expiredItemCount: 1,
    isCritical: true,
  },
  {
    id: 'loc-2',
    name: 'Armário de Reagentes A',
    type: 'ReagentCabinet',
    isActive: true,
    itemCount: 38,
    expiredItemCount: 0,
    isCritical: false,
  },
  {
    id: 'loc-3',
    name: 'Armário de Controlados',
    type: 'Controlled',
    isActive: true,
    itemCount: 9,
    expiredItemCount: 0,
    isCritical: false,
  },
  {
    id: 'loc-4',
    name: 'Estoque Geral',
    type: 'GeneralStorage',
    isActive: true,
    itemCount: 52,
    expiredItemCount: 0,
    isCritical: false,
  },
];

export const DEMO_RECENT_MOVEMENTS: RecentMovementItem[] = [
  {
    id: 'mv-1',
    stockItemId: 'si-6',
    stockItemName: 'Soro Fisiológico 0,9%',
    type: 'Consumed',
    quantity: 4,
    unit: 'frasco',
    occurredOn: isoDate(now),
    notes: 'Leva 3 — lavagem',
    estimatedCostBrl: 32.4,
  },
  {
    id: 'mv-2',
    stockItemId: 'si-1',
    stockItemName: 'Cetamina 10%',
    type: 'Consumed',
    quantity: 1,
    unit: 'frasco',
    occurredOn: isoDate(now),
    notes: 'Cirurgia',
    estimatedCostBrl: 58.0,
  },
  {
    id: 'mv-3',
    stockItemId: 'si-3',
    stockItemName: 'Formaldeído 37%',
    type: 'Received',
    quantity: 1000,
    unit: 'mL',
    occurredOn: isoDate(addDays(now, -1)),
    notes: 'Nota 8842',
    estimatedCostBrl: 210.0,
  },
  {
    id: 'mv-4',
    stockItemId: 'si-4',
    stockItemName: 'DMSO',
    type: 'Consumed',
    quantity: 15,
    unit: 'mL',
    occurredOn: isoDate(addDays(now, -2)),
    notes: 'Diluição seriada',
    estimatedCostBrl: null,
  },
  {
    id: 'mv-5',
    stockItemId: 'si-7',
    stockItemName: 'Heparina Sódica',
    type: 'Transferred',
    quantity: 1,
    unit: 'frasco',
    occurredOn: isoDate(addDays(now, -2)),
    notes: 'Geladeira → bancada',
    estimatedCostBrl: null,
  },
  {
    id: 'mv-6',
    stockItemId: 'si-8',
    stockItemName: 'Ração para roedores',
    type: 'Consumed',
    quantity: 5,
    unit: 'kg',
    occurredOn: isoDate(addDays(now, -3)),
    notes: 'Biotério',
    estimatedCostBrl: 45.0,
  },
];

// --- Dashboard --------------------------------------------------------------
export const DEMO_EXPIRY_SUMMARY: ExpirySummary = {
  expired: 2,
  expiringSoon: 6,
  ok: 120,
  total: 128,
};
export const DEMO_BELOW_MINIMUM_SUMMARY: BelowMinimumSummary = { belowMinimumCount: 4 };

export const DEMO_EXPIRING_ITEMS: ExpiringItem[] = [
  {
    id: 'si-9',
    name: 'Isoflurano',
    category: 'Anestésico',
    lotCode: 'ISO-2308',
    quantity: 1,
    unit: 'frasco',
    expiryYear: 2026,
    expiryMonth: 7,
    expiryStatus: 'Expired',
    daysRemaining: -12,
    isControlled: true,
    storageLocationId: 'loc-3',
    storageLocationName: 'Armário de Controlados',
    storageLocationType: 'Controlled',
  },
  {
    id: 'si-5',
    name: 'Kit Griess (Nitrito)',
    category: 'Kit de ensaio',
    lotCode: 'GRI-2403',
    quantity: 1,
    unit: 'kit',
    expiryYear: 2026,
    expiryMonth: 9,
    expiryStatus: 'ExpiringSoon',
    daysRemaining: 34,
    isControlled: false,
    storageLocationId: 'loc-1',
    storageLocationName: 'Geladeira 4°C — Sala 2',
    storageLocationType: 'Refrigerated',
  },
  {
    id: 'si-1',
    name: 'Cetamina 10%',
    category: 'Anestésico',
    lotCode: 'CET-2405',
    quantity: 3,
    unit: 'frasco',
    expiryYear: 2026,
    expiryMonth: 11,
    expiryStatus: 'ExpiringSoon',
    daysRemaining: 96,
    isControlled: true,
    storageLocationId: 'loc-3',
    storageLocationName: 'Armário de Controlados',
    storageLocationType: 'Controlled',
  },
  {
    id: 'si-7',
    name: 'Heparina Sódica',
    category: 'Anticoagulante',
    lotCode: 'HEP-2402',
    quantity: 2,
    unit: 'frasco',
    expiryYear: 2026,
    expiryMonth: 12,
    expiryStatus: 'ExpiringSoon',
    daysRemaining: 120,
    isControlled: false,
    storageLocationId: 'loc-1',
    storageLocationName: 'Geladeira 4°C — Sala 2',
    storageLocationType: 'Refrigerated',
  },
];

export const DEMO_BELOW_MINIMUM_ITEMS: BelowMinimumItem[] = [
  {
    id: 'si-1',
    name: 'Cetamina 10%',
    category: 'Anestésico',
    brand: 'Syntec',
    quantity: 3,
    unit: 'frasco',
    minimumQuantity: 5,
    minimumUnit: 'frasco',
    deficit: 2,
    isControlled: true,
    storageLocationId: 'loc-3',
    storageLocationName: 'Armário de Controlados',
    storageLocationType: 'Controlled',
  },
  {
    id: 'si-8',
    name: 'Ração para roedores',
    category: 'Insumo biotério',
    brand: 'Nuvilab',
    quantity: 8,
    unit: 'kg',
    minimumQuantity: 15,
    minimumUnit: 'kg',
    deficit: 7,
    isControlled: false,
    storageLocationId: 'loc-4',
    storageLocationName: 'Estoque Geral',
    storageLocationType: 'GeneralStorage',
  },
  {
    id: 'si-7',
    name: 'Heparina Sódica',
    category: 'Anticoagulante',
    brand: 'Blau',
    quantity: 2,
    unit: 'frasco',
    minimumQuantity: 6,
    minimumUnit: 'frasco',
    deficit: 4,
    isControlled: false,
    storageLocationId: 'loc-1',
    storageLocationName: 'Geladeira 4°C — Sala 2',
    storageLocationType: 'Refrigerated',
  },
];

function consumptionPoints(): ConsumptionSeries['points'] {
  const base = [42, 55, 38, 61, 70, 48, 52, 66, 74, 59, 63, 81, 77, 69];
  return base.map((totalConsumed, i) => ({
    bucketStart: isoDate(addDays(now, -(base.length - 1 - i))),
    unit: 'mL',
    totalConsumed,
  }));
}

export const DEMO_CONSUMPTION_SERIES: ConsumptionSeries = {
  bucket: 'Day',
  points: consumptionPoints(),
  totals: [{ unit: 'mL', currentTotal: 855, previousTotal: 720, deltaPercentage: 18.8 }],
};

export const DEMO_PENDENCIES: PendenciesResult = {
  awaitingCalculationCount: 2,
  pendingStepCount: 1,
  sampleAwaitingAnalysisCount: 3,
  items: [
    {
      kind: 'AwaitingCalculation',
      referenceId: 'exp-101',
      title: 'Viabilidade celular — Placa 4',
      detail: 'Leitura importada, cálculo pendente',
      sinceUtc: addDays(now, -1).toISOString(),
    },
    {
      kind: 'PendingStep',
      referenceId: 'exp-102',
      title: 'Von Frey — Leva 3',
      detail: 'Timepoint 60 min não registrado',
      sinceUtc: addDays(now, -2).toISOString(),
    },
    {
      kind: 'SampleAwaitingAnalysis',
      referenceId: 'smp-55',
      title: 'Plasma — Animal A12',
      detail: 'Sem análise concluída',
      sinceUtc: addDays(now, -3).toISOString(),
    },
  ],
};

// --- Notifications ----------------------------------------------------------
export const DEMO_UNREAD_COUNT: UnreadNotificationsCount = { unreadCount: 3 };

export const DEMO_NOTIFICATIONS: NotificationListItem[] = [
  {
    id: 'nt-1',
    type: 'ControlledCompliance',
    severity: 'Critical',
    title: 'Controlado vencido em estoque',
    description: 'Isoflurano (lote ISO-2308) venceu há 12 dias — baixa obrigatória.',
    referenceTargetType: 'stock_item',
    referenceTargetId: 'si-9',
    isRead: false,
    createdAtUtc: addDays(now, -1).toISOString(),
    readAtUtc: null,
  },
  {
    id: 'nt-2',
    type: 'ControlledCompliance',
    severity: 'Warning',
    title: 'Divergência de saldo — controlado',
    description: 'Cetamina 10%: conferência mensal pendente.',
    referenceTargetType: 'stock_item',
    referenceTargetId: 'si-1',
    isRead: false,
    createdAtUtc: addDays(now, -2).toISOString(),
    readAtUtc: null,
  },
  {
    id: 'nt-3',
    type: 'LowStock',
    severity: 'Warning',
    title: 'Estoque abaixo do mínimo',
    description: 'Ração para roedores: 8 kg (mínimo 15 kg).',
    referenceTargetType: 'stock_item',
    referenceTargetId: 'si-8',
    isRead: false,
    createdAtUtc: addDays(now, -2).toISOString(),
    readAtUtc: null,
  },
  {
    id: 'nt-4',
    type: 'Expiry',
    severity: 'Info',
    title: 'Item a vencer',
    description: 'Kit Griess vence em ~34 dias.',
    referenceTargetType: 'stock_item',
    referenceTargetId: 'si-5',
    isRead: true,
    createdAtUtc: addDays(now, -4).toISOString(),
    readAtUtc: addDays(now, -3).toISOString(),
  },
  {
    id: 'nt-5',
    type: 'Calibration',
    severity: 'Warning',
    title: 'Calibração vencida',
    description: 'Balança analítica BA-02 com calibração vencida.',
    referenceTargetType: 'equipment',
    referenceTargetId: 'eq-2',
    isRead: true,
    createdAtUtc: addDays(now, -5).toISOString(),
    readAtUtc: addDays(now, -5).toISOString(),
  },
];

// --- Agenda -----------------------------------------------------------------
function event(
  id: string,
  title: string,
  activityType: CalendarItem['activityType'],
  offsetDays: number,
  startHour: number,
  durationHours: number,
  extra: Partial<CalendarItem> = {},
): CalendarItem {
  const start = addDays(now, offsetDays);
  start.setHours(startHour, 0, 0, 0);
  const end = new Date(start);
  end.setHours(start.getHours() + durationHours);
  return {
    id,
    title,
    activityType,
    experimentId: null,
    experimentName: null,
    roomId: null,
    startDateUtc: start.toISOString(),
    endDateUtc: end.toISOString(),
    isAllDay: false,
    isRecurring: false,
    recurrenceRule: null,
    occurrenceDate: isoDate(start),
    responsibleId: 'demo-user',
    color: null,
    ...extra,
  };
}

export const DEMO_CALENDAR_ITEMS: CalendarItem[] = [
  event('ag-1', 'Von Frey — Leva 3', 'Experiment', 0, 9, 2, {
    experimentId: 'exp-102',
    experimentName: 'Von Frey — Leva 3',
  }),
  event('ag-2', 'Reunião de grupo', 'Presentation', 0, 14, 1),
  event('ag-3', 'Cirurgia — modelo de dor', 'Experiment', 1, 8, 4, {
    experimentId: 'exp-103',
    experimentName: 'Modelo de dor neuropática',
  }),
  event('ag-4', 'Plantão biotério — João', 'Bioterium', 2, 7, 12, { isAllDay: true }),
  event('ag-5', 'Seminário: artigo Nature', 'Presentation', 3, 10, 1),
  event('ag-6', 'Coleta — Biobanco', 'Experiment', -1, 13, 3, {
    experimentId: 'exp-104',
    experimentName: 'Coleta de plasma',
  }),
  event('ag-7', 'Rota-rod — Leva 2', 'Experiment', 4, 9, 2),
];
