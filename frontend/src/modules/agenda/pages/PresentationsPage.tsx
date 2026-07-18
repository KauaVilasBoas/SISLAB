import { useState } from 'react';
import { Bell, Loader2, Plus } from 'lucide-react';
import { PageHeader } from '@/shared/components/PageHeader';
import { Button } from '@/shared/components/ui/button';
import { Badge } from '@/shared/components/ui/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/shared/components/ui/card';
import { RequirePermission } from '@/modules/auth/PermissionsProvider';
import { Permissions } from '@/modules/auth/permissions';
import {
  useCancelPresentation,
  usePresentationSchedule,
} from '@/modules/agenda/api/presentations.queries';
import {
  formatDate,
  PRESENTATION_STATUS_LABEL,
  PRESENTATION_STATUS_VARIANT,
  PRESENTATION_TYPE_LABEL,
} from '@/modules/agenda/presentation';
import { SchedulePresentationModal } from '@/modules/agenda/components/SchedulePresentationModal';

/** Returns 'YYYY-MM-01' for the first day of the current semester (Jan or Jul). */
function semesterStart(): string {
  const now = new Date();
  const month = now.getMonth() < 6 ? 1 : 7;
  return `${now.getFullYear()}-${String(month).padStart(2, '0')}-01`;
}

/** Returns 'YYYY-MM-30' for the last day of the current semester (Jun or Dec). */
function semesterEnd(): string {
  const now = new Date();
  const month = now.getMonth() < 6 ? 6 : 12;
  const lastDay = new Date(now.getFullYear(), month, 0).getDate();
  return `${now.getFullYear()}-${String(month).padStart(2, '0')}-${lastDay}`;
}

/**
 * Presentations schedule page (card [E10] #71). Shows LAFTE + DOL entries for the current semester,
 * with type/status badges, reminder indicator and a permission-gated "Schedule" action.
 */
export function PresentationsPage() {
  const [from] = useState(semesterStart());
  const [to] = useState(semesterEnd());
  const [scheduling, setScheduling] = useState(false);

  const { data: items = [], isLoading } = usePresentationSchedule(from, to);
  const cancelMutation = useCancelPresentation();

  function handleCancel(id: string, title: string) {
    if (!confirm(`Cancelar apresentação "${title}"?`)) return;
    cancelMutation.mutate(id);
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title="Apresentações"
        description={`Seminários LAFTE e DOL — semestre ${from.substring(0, 7)} a ${to.substring(0, 7)}.`}
        actions={
          <RequirePermission code={Permissions.agenda.schedule}>
            <Button onClick={() => setScheduling(true)}>
              <Plus className="size-4" />
              Agendar
            </Button>
          </RequirePermission>
        }
      />

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Programação do semestre</CardTitle>
        </CardHeader>
        <CardContent>
          {isLoading ? (
            <div className="flex items-center justify-center gap-2 py-8 text-sm text-muted-foreground">
              <Loader2 className="size-4 animate-spin" />
              Carregando…
            </div>
          ) : items.length === 0 ? (
            <p className="py-8 text-center text-sm text-muted-foreground">
              Nenhuma apresentação programada para este semestre.
            </p>
          ) : (
            <table className="w-full text-sm">
              <thead className="border-b text-left text-xs uppercase tracking-wide text-muted-foreground">
                <tr>
                  <th className="px-4 py-3 font-medium">Data</th>
                  <th className="px-4 py-3 font-medium">Tipo</th>
                  <th className="px-4 py-3 font-medium">Título</th>
                  <th className="px-4 py-3 font-medium">Apresentador</th>
                  <th className="px-4 py-3 font-medium">Status</th>
                  <th className="px-4 py-3 font-medium"></th>
                </tr>
              </thead>
              <tbody>
                {items.map((p) => (
                  <tr key={p.id} className="border-b last:border-0">
                    <td className="px-4 py-3 font-medium">{formatDate(p.scheduledDate)}</td>
                    <td className="px-4 py-3 text-muted-foreground">
                      {PRESENTATION_TYPE_LABEL[p.type] ?? p.type}
                    </td>
                    <td className="px-4 py-3">
                      <span className="flex items-center gap-1">
                        {p.title}
                        {p.reminderSent && (
                          <Bell className="size-3.5 text-muted-foreground" aria-label="Lembrete enviado" />
                        )}
                      </span>
                      {p.doi && (
                        <span className="text-xs text-muted-foreground">DOI: {p.doi}</span>
                      )}
                    </td>
                    <td className="px-4 py-3 text-muted-foreground">{p.presenterName}</td>
                    <td className="px-4 py-3">
                      <Badge variant={PRESENTATION_STATUS_VARIANT[p.status]}>
                        {PRESENTATION_STATUS_LABEL[p.status]}
                      </Badge>
                    </td>
                    <td className="px-4 py-3 text-right">
                      {p.status === 'Scheduled' && (
                        <RequirePermission code={Permissions.agenda.cancelPresentation}>
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={() => handleCancel(p.id, p.title)}
                            disabled={cancelMutation.isPending}
                          >
                            Cancelar
                          </Button>
                        </RequirePermission>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </CardContent>
      </Card>

      {scheduling && (
        <SchedulePresentationModal
          onClose={() => setScheduling(false)}
          onScheduled={() => setScheduling(false)}
        />
      )}
    </div>
  );
}
