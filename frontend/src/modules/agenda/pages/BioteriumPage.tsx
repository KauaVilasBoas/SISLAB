import { useState } from 'react';
import { Loader2 } from 'lucide-react';
import { PageHeader } from '@/shared/components/PageHeader';
import { Button } from '@/shared/components/ui/button';
import { Badge } from '@/shared/components/ui/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/shared/components/ui/card';
import { RequirePermission } from '@/modules/auth/PermissionsProvider';
import { Permissions } from '@/modules/auth/permissions';
import {
  useBioteriumSchedule,
  useGenerateBioteriumWeek,
  useMarkDone,
  useSwapAssignment,
} from '@/modules/agenda/api/bioterium.queries';
import {
  ASSIGNMENT_STATUS_LABEL,
  ASSIGNMENT_STATUS_VARIANT,
  currentWeekMonday,
  formatDate,
} from '@/modules/agenda/presentation';
import type { BioteriumAssignmentItem } from '@/modules/agenda/types';

/** Returns the Sunday of the week starting on a given Monday (ISO 'YYYY-MM-DD'). */
function weekEnd(monday: string): string {
  const d = new Date(monday);
  d.setDate(d.getDate() + 6);
  return d.toISOString().substring(0, 10);
}

/**
 * Biotério cage-cleaning schedule page (card [E10] #70). Displays the week's Mon+Thu assignments,
 * allows generating the next week and swapping / marking done.
 */
export function BioteriumPage() {
  const [monday, setMonday] = useState(currentWeekMonday());
  const to = weekEnd(monday);

  const { data: assignments = [], isLoading } = useBioteriumSchedule(monday, to);
  const generateMutation = useGenerateBioteriumWeek();
  const swapMutation = useSwapAssignment();
  const doneMutation = useMarkDone();

  function shiftWeek(weeks: number) {
    const d = new Date(monday);
    d.setDate(d.getDate() + weeks * 7);
    setMonday(d.toISOString().substring(0, 10));
  }

  function handleGenerate() {
    const names = prompt('Nomes dos responsáveis separados por vírgula (ex: Ana, Carlos, Marina):');
    if (!names) return;
    const responsibleNames = names.split(',').map((n) => n.trim()).filter(Boolean);
    if (responsibleNames.length === 0) return;
    generateMutation.mutate({ mondayOfWeek: monday, responsibleNames });
  }

  function handleSwap(assignment: BioteriumAssignmentItem) {
    const newName = prompt(`Permutar "${assignment.responsibleName}" por:`);
    if (!newName?.trim()) return;
    const reason = prompt('Motivo (opcional):') ?? undefined;
    swapMutation.mutate({ assignmentId: assignment.id, newResponsibleName: newName.trim(), reason });
  }

  function handleDone(assignment: BioteriumAssignmentItem) {
    if (!confirm(`Marcar ${formatDate(assignment.assignmentDate)} como realizado?`)) return;
    doneMutation.mutate({ assignmentId: assignment.id });
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title="Troca do biotério"
        description="Escala de limpeza das caixas — segunda e quinta, rodízio semanal."
        actions={
          <RequirePermission code={Permissions.agenda.generateWeek}>
            <Button
              variant="outline"
              onClick={handleGenerate}
              disabled={generateMutation.isPending}
            >
              Gerar semana
            </Button>
          </RequirePermission>
        }
      />

      <div className="flex items-center gap-3">
        <Button variant="outline" size="sm" onClick={() => shiftWeek(-1)}>← Semana anterior</Button>
        <span className="text-sm font-medium">
          {formatDate(monday)} – {formatDate(to)}
        </span>
        <Button variant="outline" size="sm" onClick={() => shiftWeek(1)}>Próxima semana →</Button>
        <Button variant="ghost" size="sm" onClick={() => setMonday(currentWeekMonday())}>Esta semana</Button>
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Responsáveis da semana</CardTitle>
        </CardHeader>
        <CardContent>
          {isLoading ? (
            <div className="flex items-center justify-center gap-2 py-8 text-sm text-muted-foreground">
              <Loader2 className="size-4 animate-spin" />
              Carregando…
            </div>
          ) : assignments.length === 0 ? (
            <p className="py-8 text-center text-sm text-muted-foreground">
              Nenhuma escala gerada para esta semana.{' '}
              <RequirePermission code={Permissions.agenda.generateWeek}>
                <button className="text-primary underline" onClick={handleGenerate}>
                  Gerar agora
                </button>
              </RequirePermission>
            </p>
          ) : (
            <table className="w-full text-sm">
              <thead className="border-b text-left text-xs uppercase tracking-wide text-muted-foreground">
                <tr>
                  <th className="px-4 py-3 font-medium">Data</th>
                  <th className="px-4 py-3 font-medium">Responsável</th>
                  <th className="px-4 py-3 font-medium">Status</th>
                  <th className="px-4 py-3 font-medium"></th>
                </tr>
              </thead>
              <tbody>
                {assignments.map((a) => (
                  <tr key={a.id} className="border-b last:border-0">
                    <td className="px-4 py-3 font-medium">{formatDate(a.assignmentDate)}</td>
                    <td className="px-4 py-3">
                      {a.responsibleName}
                      {a.swappedFromName && (
                        <span className="ml-1 text-xs text-muted-foreground">
                          (era {a.swappedFromName})
                        </span>
                      )}
                    </td>
                    <td className="px-4 py-3">
                      <Badge variant={ASSIGNMENT_STATUS_VARIANT[a.status]}>
                        {ASSIGNMENT_STATUS_LABEL[a.status]}
                      </Badge>
                    </td>
                    <td className="px-4 py-3">
                      <div className="flex justify-end gap-2">
                        {a.status !== 'Done' && (
                          <>
                            <RequirePermission code={Permissions.agenda.swap}>
                              <Button
                                variant="ghost"
                                size="sm"
                                onClick={() => handleSwap(a)}
                                disabled={swapMutation.isPending}
                              >
                                Permutar
                              </Button>
                            </RequirePermission>
                            <RequirePermission code={Permissions.agenda.markDone}>
                              <Button
                                variant="outline"
                                size="sm"
                                onClick={() => handleDone(a)}
                                disabled={doneMutation.isPending}
                              >
                                Marcar como feito
                              </Button>
                            </RequirePermission>
                          </>
                        )}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
