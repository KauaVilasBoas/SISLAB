import { setupWorker } from 'msw/browser';
import { demoHandlers } from '@/demo/handlers';

/**
 * The demo's Mock Service Worker. Imported dynamically from `main.tsx` ONLY when `IS_DEMO`, so neither
 * MSW nor the fixtures are pulled into the real (backend-connected) production bundle.
 */
export const worker = setupWorker(...demoHandlers);
