import { http, HttpResponse } from 'msw';
import type { ApiResult } from '@/shared/types/api';
import {
  DEMO_ACTIVE_COMPANY,
  DEMO_COMPANY,
  DEMO_LOGIN_RESULT,
  DEMO_PERMISSIONS,
  DEMO_USER,
} from '@/demo/session';
import {
  DEMO_BELOW_MINIMUM_ITEMS,
  DEMO_BELOW_MINIMUM_SUMMARY,
  DEMO_CALENDAR_ITEMS,
  DEMO_CONSUMPTION_SERIES,
  DEMO_EXPIRING_ITEMS,
  DEMO_EXPIRY_SUMMARY,
  DEMO_LOCATIONS,
  DEMO_NOTIFICATIONS,
  DEMO_PENDENCIES,
  DEMO_RECENT_MOVEMENTS,
  DEMO_STOCK_ITEMS,
  DEMO_UNREAD_COUNT,
  paged,
} from '@/demo/fixtures';
import { computeDilutionScheme } from '@/demo/dilution';

/** Wrap a payload in the SISLAB success envelope (module read endpoints use it; auth endpoints don't). */
function ok<T>(data: T): ApiResult<T> {
  return { success: true, message: null, data };
}

/** The standard "this is a read-only demo" refusal for any state-changing request. */
function readOnly() {
  return HttpResponse.json(
    {
      success: false,
      message: 'Ação indisponível na demonstração (somente leitura).',
      data: null,
    },
    { status: 403 },
  );
}

/**
 * A tiny demo "session", persisted in localStorage so the login screen is real: the app boots
 * UNAUTHENTICATED (GET /api/me → 401) and only signs in once the visitor clicks "Entrar na
 * demonstração" (POST /api/auth/login flips the flag). A refresh keeps the session; logout clears it.
 */
const DEMO_SESSION_KEY = 'sislab_demo_session';
const isLoggedIn = () => {
  try {
    return localStorage.getItem(DEMO_SESSION_KEY) === '1';
  } catch {
    return false;
  }
};
const setLoggedIn = (value: boolean) => {
  try {
    if (value) localStorage.setItem(DEMO_SESSION_KEY, '1');
    else localStorage.removeItem(DEMO_SESSION_KEY);
  } catch {
    // Ignore — a demo without storage still works, it just always starts signed out.
  }
};
const unauthorized = () => new HttpResponse(null, { status: 401 });

/**
 * MSW request handlers for the backend-less demo.
 *
 * Ordered specific-first — MSW resolves with the first matching handler, so the auth + write handlers must
 * be declared before the catch-alls. Everything is read-only: unsafe methods (except the auth POSTs that
 * drive the login flow) return a friendly refusal that surfaces through the app's existing error toast.
 *
 * NOTE: the trailing GET catch-all returns an empty envelope so un-seeded screens render their empty state
 * instead of crashing. Hero screens (dashboard, inventory, agenda) get dedicated seeded fixtures ABOVE this
 * line as they are built out.
 */
export const demoHandlers = [
  // --- Auth bootstrap + login flow (raw bodies, no envelope) --------------
  http.get('/api/auth/csrf', () => new HttpResponse(null, { status: 204 })),
  http.get('/api/me/permissions', () =>
    isLoggedIn() ? HttpResponse.json(DEMO_PERMISSIONS) : unauthorized(),
  ),
  http.get('/api/me', () =>
    isLoggedIn() ? HttpResponse.json(DEMO_USER) : unauthorized(),
  ),
  http.get('/api/companies/mine', () => HttpResponse.json([DEMO_COMPANY])),
  http.get('/api/companies/active', () =>
    isLoggedIn()
      ? HttpResponse.json(DEMO_ACTIVE_COMPANY)
      : new HttpResponse(null, { status: 404 }),
  ),
  http.post('/api/auth/login', () => {
    setLoggedIn(true);
    return HttpResponse.json(DEMO_LOGIN_RESULT);
  }),
  http.post('/api/auth/logout', () => {
    setLoggedIn(false);
    return new HttpResponse(null, { status: 204 });
  }),
  http.post(
    '/api/companies/:companyId/activate',
    () => new HttpResponse(null, { status: 204 }),
  ),

  // --- Seeded read fixtures for the hero screens -------------------------
  // Dashboard (Inventory read-side summaries + alerts + experiment pendencies).
  http.get('/api/inventory/stock-items/expiry-summary', () =>
    HttpResponse.json(ok(DEMO_EXPIRY_SUMMARY)),
  ),
  http.get('/api/inventory/stock-items/below-minimum/summary', () =>
    HttpResponse.json(ok(DEMO_BELOW_MINIMUM_SUMMARY)),
  ),
  http.get('/api/inventory/stock-items/expiring', () =>
    HttpResponse.json(ok(paged(DEMO_EXPIRING_ITEMS))),
  ),
  http.get('/api/inventory/stock-items/below-minimum', () =>
    HttpResponse.json(ok(paged(DEMO_BELOW_MINIMUM_ITEMS))),
  ),
  http.get('/api/inventory/consumption-series', () =>
    HttpResponse.json(ok(DEMO_CONSUMPTION_SERIES)),
  ),
  // No overdue-calibration equipment in the demo — a clean, positive state (empty paged, not a crash).
  http.get('/api/inventory/equipment', () => HttpResponse.json(ok(paged([])))),
  http.get('/api/experiments/pendencies', () => HttpResponse.json(ok(DEMO_PENDENCIES))),
  // Experiments list is Premium-gated in the UI; the Agenda experiment filter still queries it — empty paged.
  http.get('/api/experiments', () => HttpResponse.json(ok(paged([])))),
  // Serial-dilution calculator (Core, free): compute the REAL scheme from the query string, no backend.
  http.get('/api/experiments/dilution-scheme', ({ request }) =>
    HttpResponse.json(ok(computeDilutionScheme(new URL(request.url).searchParams))),
  ),

  // Inventory (Estoque) screen: item table, location sidebar, recent-activity panel.
  http.get('/api/inventory/stock-items', () =>
    HttpResponse.json(ok(paged(DEMO_STOCK_ITEMS))),
  ),
  http.get('/api/inventory/storage-locations/summary', () =>
    HttpResponse.json(ok(DEMO_LOCATIONS)),
  ),
  http.get('/api/inventory/stock-movements/recent', () =>
    HttpResponse.json(ok(DEMO_RECENT_MOVEMENTS)),
  ),

  // Agenda (Calendário) screen.
  http.get('/api/agenda/calendar', () => HttpResponse.json(ok(DEMO_CALENDAR_ITEMS))),

  // Notifications (topbar bell, always mounted + the dashboard compliance widget).
  http.get('/api/notifications/unread-count', () =>
    HttpResponse.json(ok(DEMO_UNREAD_COUNT)),
  ),
  http.get('/api/notifications', () => HttpResponse.json(ok(paged(DEMO_NOTIFICATIONS)))),

  // --- Read-only guard: every other mutation is disabled in the demo -----
  http.post('/api/*', () => readOnly()),
  http.put('/api/*', () => readOnly()),
  http.patch('/api/*', () => readOnly()),
  http.delete('/api/*', () => readOnly()),

  // --- Read catch-all: empty-but-valid envelope so screens don't crash ---
  http.get('/api/*', () => HttpResponse.json(ok([]))),
];
