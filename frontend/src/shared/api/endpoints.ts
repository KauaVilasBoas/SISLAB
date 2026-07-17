/**
 * Single source of truth for API routes.
 *
 * Mirrors the backend module/controller layout (Identity, Inventory, Configuration,
 * Audit, Notifications). Parameterized routes are functions; static routes are strings.
 * Never hardcode a path in a component or query — reference it here so a backend
 * route change is a one-line edit.
 *
 * Paths are relative (start with /api) and resolved against httpClient.baseURL.
 */
export const Endpoints = {
  identity: {
    /** Session auth (card [E7] #44) — httpOnly cookie bridge over Lumen. */
    auth: {
      login: '/api/auth/login',
      logout: '/api/auth/logout',
      refresh: '/api/auth/refresh',
      me: '/api/me',
      /** Effective permission codes of the signed-in user in the active company (front permission gate). */
      myPermissions: '/api/me/permissions',
      /** Arms the readable XSRF-TOKEN cookie for the double-submit CSRF defense. */
      csrf: '/api/auth/csrf',
    },
    /** Active-company selection/switching (post-login, httpOnly cookie). */
    activeCompany: {
      mine: '/api/companies/mine',
      activate: (companyId: string) => `/api/companies/${companyId}/activate`,
      active: '/api/companies/active',
    },
    /** Public self-service onboarding — POST company signup with coordinator. */
    companies: {
      signup: '/api/companies/signup',
    },
    /** Public invitation flow (anonymous): preview + accept. */
    invitations: {
      preview: (token: string) => `/api/companies/invitations/${token}`,
      accept: '/api/companies/invitations/accept',
    },
    /** Admin — members of the active company. */
    members: {
      list: '/api/admin/companies/active/members',
      enriched: '/api/admin/companies/active/members/enriched',
      removalEligibility: (userId: string) =>
        `/api/admin/companies/active/members/${userId}/removal-eligibility`,
      invite: '/api/admin/companies/active/members/invite',
      profiles: (userId: string) =>
        `/api/admin/companies/active/members/${userId}/profiles`,
      profile: (userId: string, profileId: string) =>
        `/api/admin/companies/active/members/${userId}/profiles/${profileId}`,
    },
    /** Admin — company-scoped profiles (Lumen) and permission catalogue. */
    profiles: {
      root: '/api/admin/profiles',
      availablePermissions: '/api/admin/profiles/permissions',
      byId: (profileId: string) => `/api/admin/profiles/${profileId}`,
      permissions: (profileId: string) => `/api/admin/profiles/${profileId}/permissions`,
    },
  },

  inventory: {
    stockItems: {
      list: '/api/inventory/stock-items',
      create: '/api/inventory/stock-items',
      /** Single item card (name, lot, balance, storage location) — feeds the mobile QR flow (#63). */
      byId: (stockItemId: string) => `/api/inventory/stock-items/${stockItemId}`,
      expiring: '/api/inventory/stock-items/expiring',
      expirySummary: '/api/inventory/stock-items/expiry-summary',
      belowMinimum: '/api/inventory/stock-items/below-minimum',
      belowMinimumSummary: '/api/inventory/stock-items/below-minimum/summary',
      entries: (stockItemId: string) =>
        `/api/inventory/stock-items/${stockItemId}/entries`,
      consumptions: (stockItemId: string) =>
        `/api/inventory/stock-items/${stockItemId}/consumptions`,
      transfers: (stockItemId: string) =>
        `/api/inventory/stock-items/${stockItemId}/transfers`,
      disposals: (stockItemId: string) =>
        `/api/inventory/stock-items/${stockItemId}/disposals`,
      counts: (stockItemId: string) => `/api/inventory/stock-items/${stockItemId}/counts`,
      movements: (stockItemId: string) =>
        `/api/inventory/stock-items/${stockItemId}/movements`,
    },
    stockBatches: {
      /** Available batches (remaining > 0) of an item, FEFO-ordered — feeds the consumption lot picker (#111). */
      byItem: (stockItemId: string) => `/api/inventory/stock-batches/${stockItemId}`,
    },
    stockMovements: {
      /** Cross-item recent-activity feed (latest N movements of the active company) — card [E7] #47. */
      recent: '/api/inventory/stock-movements/recent',
    },
    storageLocations: {
      /** Item-browser sidebar summary (item/expired counts, critical flag) — card [E7] #46. */
      summary: '/api/inventory/storage-locations/summary',
      /** Flat management listing + create (card [E7] #112). */
      root: '/api/inventory/storage-locations',
      byId: (id: string) => `/api/inventory/storage-locations/${id}`,
      status: (id: string) => `/api/inventory/storage-locations/${id}/status`,
    },
    reports: {
      consumption: '/api/inventory/consumption-report',
      consumptionSeries: '/api/inventory/consumption-series',
      /** Monthly consumption cost of the active company (Inventory.Cost.Read) — card [E4] #109. */
      costByMonth: '/api/inventory/reports/cost-by-month',
      /** Consumption cost per experiment, top-N (Inventory.Cost.Read) — card [E4] #109. */
      costByExperiment: '/api/inventory/reports/cost-by-experiment',
    },
    partners: {
      root: '/api/inventory/partners',
      byId: (id: string) => `/api/inventory/partners/${id}`,
      deactivate: (id: string) => `/api/inventory/partners/${id}/deactivation`,
      reactivate: (id: string) => `/api/inventory/partners/${id}/reactivation`,
      samples: (id: string) => `/api/inventory/partners/${id}/samples`,
      sample: (id: string, reference: string) =>
        `/api/inventory/partners/${id}/samples/${reference}`,
    },
    equipment: {
      root: '/api/inventory/equipment',
      byId: (id: string) => `/api/inventory/equipment/${id}`,
      status: (id: string) => `/api/inventory/equipment/${id}/status`,
      calibration: (id: string) => `/api/inventory/equipment/${id}/calibration`,
      maintenances: (id: string) => `/api/inventory/equipment/${id}/maintenances`,
    },
  },

  configuration: {
    expiryPolicy: '/api/configuration/expiry-policy',
    itemCategories: '/api/configuration/item-categories',
    referenceRanges: '/api/configuration/reference-ranges',
    rooms: '/api/configuration/rooms',
    units: '/api/configuration/units',
  },

  audit: {
    entries: '/api/audit/entries',
    entriesExport: '/api/audit/entries/export',
  },

  notifications: {
    list: '/api/notifications',
    unreadCount: '/api/notifications/unread-count',
    markRead: (notificationId: string) => `/api/notifications/${notificationId}/read`,
    readAll: '/api/notifications/read-all',
  },
} as const;
