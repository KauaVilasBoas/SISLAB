import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import App from './App';
import './index.css';
import { IS_DEMO } from '@/demo/isDemo';

/**
 * App entry. In the backend-less demo build we start the Mock Service Worker BEFORE the first render, so the
 * AuthProvider bootstrap (which fires immediately) already finds the mocked `/api/*` handlers in place.
 * The worker module is imported dynamically so MSW is absent from the real production bundle.
 */
async function bootstrap() {
  if (IS_DEMO) {
    const { worker } = await import('@/demo/browser');
    await worker.start({ onUnhandledRequest: 'bypass', quiet: true });
  }

  createRoot(document.getElementById('root')!).render(
    <StrictMode>
      <App />
    </StrictMode>,
  );
}

void bootstrap();
