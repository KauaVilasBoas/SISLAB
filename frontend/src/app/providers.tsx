import { useState, type ReactNode } from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ApiError } from '@/shared/types/api';
import { AuthProvider } from '@/modules/auth/AuthProvider';
import { PermissionsProvider } from '@/modules/auth/PermissionsProvider';
import { ThemeProvider } from '@/app/theme/ThemeProvider';
import { ToastProvider } from '@/shared/components/ui/toast';

/**
 * App-wide providers. Add new context providers (theme, auth, toaster) here so
 * main.tsx stays a thin bootstrap.
 *
 * AuthProvider sits INSIDE QueryClientProvider (it uses useQueryClient to reset the
 * session cache on login/logout/company-switch) and OUTSIDE the router, so the whole
 * route tree — public /login included — can read the auth context.
 *
 * PermissionsProvider sits just below AuthProvider (it reads the auth status + active company
 * to fetch the effective permissions) and above the router, so any screen can gate UI on a
 * permission via usePermissions()/<RequirePermission>.
 */
export function AppProviders({ children }: { children: ReactNode }) {
  const [queryClient] = useState(
    () =>
      new QueryClient({
        defaultOptions: {
          queries: {
            staleTime: 30_000,
            retry: (failureCount, error) => {
              // Don't retry auth/permission/validation errors — only transient ones.
              const status = (error as unknown as ApiError)?.status;
              if (status && status >= 400 && status < 500) return false;
              return failureCount < 2;
            },
            refetchOnWindowFocus: false,
          },
        },
      }),
  );

  return (
    <ThemeProvider>
      <QueryClientProvider client={queryClient}>
        <ToastProvider>
          <AuthProvider>
            <PermissionsProvider>{children}</PermissionsProvider>
          </AuthProvider>
        </ToastProvider>
      </QueryClientProvider>
    </ThemeProvider>
  );
}
