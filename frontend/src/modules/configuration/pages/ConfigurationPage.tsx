import { useState } from 'react';
import { PageHeader } from '@/shared/components/PageHeader';
import { cn } from '@/shared/lib/utils';
import { UnitsTab } from '@/modules/configuration/components/UnitsTab';
import { RoomsTab } from '@/modules/configuration/components/RoomsTab';
import { ItemCategoriesTab } from '@/modules/configuration/components/ItemCategoriesTab';
import { ReferenceRangesTab } from '@/modules/configuration/components/ReferenceRangesTab';
import { ExpiryPolicyTab } from '@/modules/configuration/components/ExpiryPolicyTab';

type TabKey = 'units' | 'rooms' | 'categories' | 'ranges' | 'expiry';

const TABS: { key: TabKey; label: string }[] = [
  { key: 'units', label: 'Unidades de medida' },
  { key: 'rooms', label: 'Salas' },
  { key: 'categories', label: 'Categorias de itens' },
  { key: 'ranges', label: 'Faixas de referência' },
  { key: 'expiry', label: 'Política de validade' },
];

/**
 * Laboratory configuration mother screen (card [E12] #76). Owns the tab state and composes the five
 * catalogue tabs; each tab owns its own query, create flow and cache invalidation, so the page stays
 * a thin shell around the shared tab strip and PageHeader.
 */
export function ConfigurationPage() {
  const [tab, setTab] = useState<TabKey>('units');

  return (
    <div className="space-y-6">
      <PageHeader
        title="Configurações"
        description="Catálogos do laboratório: unidades, salas, categorias, faixas de referência e política de validade."
      />

      <div
        role="tablist"
        aria-label="Configurações do laboratório"
        className="inline-flex flex-wrap gap-1 rounded-lg bg-muted p-1"
      >
        {TABS.map(({ key, label }) => (
          <button
            key={key}
            role="tab"
            type="button"
            aria-selected={tab === key}
            onClick={() => setTab(key)}
            className={cn(
              'rounded-md px-4 py-1.5 text-sm font-medium transition-colors',
              tab === key
                ? 'bg-card text-foreground shadow-sm'
                : 'text-muted-foreground hover:text-foreground',
            )}
          >
            {label}
          </button>
        ))}
      </div>

      {tab === 'units' ? <UnitsTab /> : null}
      {tab === 'rooms' ? <RoomsTab /> : null}
      {tab === 'categories' ? <ItemCategoriesTab /> : null}
      {tab === 'ranges' ? <ReferenceRangesTab /> : null}
      {tab === 'expiry' ? <ExpiryPolicyTab /> : null}
    </div>
  );
}
