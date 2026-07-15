import { Building2, Loader2 } from 'lucide-react';
import { useAuth } from '@/modules/auth/AuthProvider';
import { useMyCompanies } from '@/modules/auth/api/auth.queries';

/**
 * Topbar company switcher (card [E7] #44). Lets a multi-company user change the active company
 * without re-login: POST /api/companies/{id}/activate rewrites the httpOnly active-company cookie,
 * and the AuthProvider clears tenant-scoped caches so screens refetch under the new company.
 *
 * Renders as a native <select> (no extra dropdown primitive dependency). Hidden entirely when the
 * user belongs to a single company — there is nothing to switch.
 */
export function CompanySwitcher() {
  const { activeCompanyId, selectCompany, status } = useAuth();
  const { data: companies, isPending } = useMyCompanies(status === 'authenticated');

  if (!companies || companies.length <= 1) return null;

  return (
    <label className="flex items-center gap-2 text-sm">
      <Building2 className="size-4 text-muted-foreground" aria-hidden />
      <span className="sr-only">Empresa ativa</span>
      <div className="relative">
        <select
          className="h-8 rounded-md border border-input bg-background pl-2 pr-7 text-sm shadow-sm focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring disabled:opacity-50"
          value={activeCompanyId ?? ''}
          disabled={isPending}
          onChange={(e) => void selectCompany(e.target.value)}
        >
          {activeCompanyId === null && <option value="">Selecionar empresa…</option>}
          {companies.map((company) => (
            <option key={company.id} value={company.id}>
              {company.name}
            </option>
          ))}
        </select>
        {isPending && (
          <Loader2
            className="pointer-events-none absolute right-2 top-1.5 size-4 animate-spin text-muted-foreground"
            aria-hidden
          />
        )}
      </div>
    </label>
  );
}
