import { useState } from 'react';
import { useLocation } from 'react-router-dom';
import { PageHeader } from '@/shared/components/PageHeader';
import { cn } from '@/shared/lib/utils';
import { useProfiles } from '@/modules/identity/api/identity.queries';
import { MembersTab } from '@/modules/identity/components/MembersTab';
import { ProfilesTab } from '@/modules/identity/components/ProfilesTab';

type TabKey = 'members' | 'profiles';

const TABS: { key: TabKey; label: string }[] = [
  { key: 'members', label: 'Membros' },
  { key: 'profiles', label: 'Perfis' },
];

/**
 * Members & Profiles mother screen (card [E7] #105). Owns the tab state and the profiles query
 * (shared by both tabs — the "Members" tab needs the profile list for invite and per-member
 * assignment), and composes the two presentational tabs.
 *
 * When navigating back from ProfileEditPage the router state carries { tab: 'profiles' } so the
 * user lands on the tab they came from instead of the default Members tab.
 */
export function MembersPage() {
  const location = useLocation();
  const initialTab = (location.state as { tab?: TabKey } | null)?.tab ?? 'members';
  const [tab, setTab] = useState<TabKey>(initialTab);
  const profiles = useProfiles();

  return (
    <div className="space-y-6">
      <PageHeader
        title="Membros & Perfis"
        description="Convide membros, atribua perfis e gerencie as permissões da empresa."
      />

      <div
        role="tablist"
        aria-label="Membros e Perfis"
        className="inline-flex gap-1 rounded-lg bg-muted p-1"
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

      {tab === 'members' ? (
        <MembersTab profiles={profiles.data ?? []} />
      ) : (
        <ProfilesTab />
      )}
    </div>
  );
}
