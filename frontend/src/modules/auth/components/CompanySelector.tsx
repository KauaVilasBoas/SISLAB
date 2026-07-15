import { useState } from 'react';
import { Loader2 } from 'lucide-react';
import { Button } from '@/shared/components/ui/button';
import type { CompanyMembership } from '@/modules/auth/types';

/**
 * Post-login company picker (card [E7] #44) — shown only when the user belongs to more than one
 * company. Presentational: the mother screen (LoginPage) owns activation and navigation.
 */
export function CompanySelector({
  companies,
  onSelect,
}: {
  companies: CompanyMembership[];
  onSelect: (companyId: string) => Promise<void> | void;
}) {
  const [pendingId, setPendingId] = useState<string | null>(null);

  async function handleSelect(companyId: string) {
    setPendingId(companyId);
    try {
      await onSelect(companyId);
    } finally {
      setPendingId(null);
    }
  }

  return (
    <div className="flex flex-col gap-3">
      <p className="text-sm text-muted-foreground">Selecione a empresa para continuar.</p>

      <ul className="flex flex-col gap-2">
        {companies.map((company) => (
          <li key={company.id}>
            <Button
              type="button"
              variant="outline"
              className="w-full justify-between"
              disabled={pendingId !== null}
              onClick={() => handleSelect(company.id)}
            >
              <span className="truncate">{company.name}</span>
              {pendingId === company.id && (
                <Loader2 className="size-4 animate-spin" aria-hidden />
              )}
            </Button>
          </li>
        ))}
      </ul>
    </div>
  );
}
