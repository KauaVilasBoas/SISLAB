/**
 * True when the SPA is built as the public, backend-less demo (deployed to Vercel with `VITE_DEMO=true`).
 *
 * In demo mode `main.tsx` boots a Mock Service Worker that answers every `/api/*` call with seeded,
 * read-only fixtures — so the app runs with no backend, no database and no exposure of the real client's
 * data. In every other build this is `false` and the app talks to the real ASP.NET API as usual.
 */
export const IS_DEMO = import.meta.env.VITE_DEMO === 'true';
