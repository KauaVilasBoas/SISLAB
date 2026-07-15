import {
  createContext,
  useCallback,
  useContext,
  useState,
  type ReactNode,
} from 'react';
import { createPortal } from 'react-dom';
import { CheckCircle2, X, XCircle } from 'lucide-react';
import { cn } from '@/shared/lib/utils';

export type ToastType = 'success' | 'error';

interface ToastItem {
  id: number;
  type: ToastType;
  message: string;
}

type ToastFn = (type: ToastType, message: string) => void;

const ToastContext = createContext<ToastFn | null>(null);

let _nextId = 0;

const DURATION: Record<ToastType, number> = { success: 3000, error: 5000 };

export function ToastProvider({ children }: { children: ReactNode }) {
  const [items, setItems] = useState<ToastItem[]>([]);

  const dismiss = useCallback((id: number) => {
    setItems((prev) => prev.filter((t) => t.id !== id));
  }, []);

  const toast = useCallback<ToastFn>(
    (type, message) => {
      const id = ++_nextId;
      // Keep at most 5 toasts; push new one at the end (visual bottom = latest).
      setItems((prev) => [...prev.slice(-4), { id, type, message }]);
      setTimeout(() => dismiss(id), DURATION[type]);
    },
    [dismiss],
  );

  return (
    <ToastContext.Provider value={toast}>
      {children}
      {createPortal(
        <div
          aria-live="polite"
          aria-atomic="false"
          className="fixed bottom-4 right-4 z-[200] flex w-80 flex-col gap-2"
        >
          {items.map((item) => (
            <div
              key={item.id}
              role={item.type === 'error' ? 'alert' : 'status'}
              className={cn(
                'flex items-start gap-3 rounded-lg border px-4 py-3 text-sm shadow-lg',
                'animate-in slide-in-from-bottom-2 fade-in duration-200',
                item.type === 'success'
                  ? [
                      'border-emerald-200 bg-emerald-50 text-emerald-900',
                      'dark:border-emerald-800 dark:bg-emerald-950 dark:text-emerald-100',
                    ]
                  : [
                      'border-destructive/30 bg-destructive/10 text-foreground',
                      'dark:bg-destructive/20 dark:border-destructive/40',
                    ],
              )}
            >
              {item.type === 'success' ? (
                <CheckCircle2 className="mt-0.5 size-4 shrink-0 text-emerald-600 dark:text-emerald-400" />
              ) : (
                <XCircle className="mt-0.5 size-4 shrink-0 text-destructive" />
              )}
              <span className="flex-1">{item.message}</span>
              <button
                type="button"
                aria-label="Fechar notificação"
                onClick={() => dismiss(item.id)}
                className="mt-0.5 shrink-0 rounded opacity-60 transition-opacity hover:opacity-100"
              >
                <X className="size-3.5" />
              </button>
            </div>
          ))}
        </div>,
        document.body,
      )}
    </ToastContext.Provider>
  );
}

export function useToast(): ToastFn {
  const ctx = useContext(ToastContext);
  if (!ctx) throw new Error('useToast must be used within <ToastProvider>');
  return ctx;
}
