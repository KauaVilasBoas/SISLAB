import { ShieldAlert } from 'lucide-react';
import { PageHeader } from '@/shared/components/PageHeader';
import { Card, CardContent } from '@/shared/components/ui/card';

interface NoPermissionStateProps {
  /** Page title kept so the chrome still reads coherently (defaults to "Acesso restrito"). */
  title?: string;
  /** Optional line explaining which capability is missing. */
  description?: string;
}

/**
 * "Sem permissão" state for a permission-gated route reached directly by URL (card [E7] #110). The sidebar
 * already hides links the user cannot use, but a user can still type the path or follow a stale bookmark —
 * this renders a calm, explanatory panel instead of a broken screen or a 403 toast loop. The backend stays
 * the authority: even reaching the route, its gated read endpoints would 403.
 */
export function NoPermissionState({
  title = 'Acesso restrito',
  description = 'Você não tem permissão para acessar esta área nesta empresa. Fale com um coordenador se precisar de acesso.',
}: NoPermissionStateProps) {
  return (
    <div className="space-y-6">
      <PageHeader title={title} />
      <Card>
        <CardContent className="flex flex-col items-center justify-center gap-3 py-16 text-center">
          <ShieldAlert className="size-8 text-muted-foreground" />
          <p className="max-w-md text-sm text-muted-foreground">{description}</p>
        </CardContent>
      </Card>
    </div>
  );
}
