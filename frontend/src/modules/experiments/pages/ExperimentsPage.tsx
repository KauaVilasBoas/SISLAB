import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Plus, Loader2 } from 'lucide-react';
import { PageHeader } from '@/shared/components/PageHeader';
import { Button } from '@/shared/components/ui/button';
import { Badge } from '@/shared/components/ui/badge';
import { cn } from '@/shared/lib/utils';
import { useExperiments } from '@/modules/experiments/api/experiments.queries';
import { CreateExperimentModal } from '@/modules/experiments/components/CreateExperimentModal';
import {
  experimentStatusPresentation,
  typePresentation,
  formatDate,
} from '@/modules/experiments/components/experiment-presentation';
import type { ExperimentStatus } from '@/modules/experiments/types';

const PAGE_SIZE = 20;

const STATUS_FILTERS: { value: string; label: string }[] = [
  { value: '', label: 'Todos' },
  { value: 'Draft', label: 'Rascunho' },
  { value: 'InProgress', label: 'Em andamento' },
  { value: 'AwaitingAnalysis', label: 'Aguardando análise' },
  { value: 'Completed', label: 'Concluído' },
  { value: 'Archived', label: 'Arquivado' },
];

/**
 * Experiments list (card [E11] #68 — in vitro viability slice). A paginated table of the active
 * company's experiments with a status filter and a "New experiment" action; each row navigates to the
 * detail. Thin shell around the list query, the status filter and the create modal.
 */
export function ExperimentsPage() {
  const navigate = useNavigate();
  const [page, setPage] = useState(1);
  const [status, setStatus] = useState('');
  const [creating, setCreating] = useState(false);

  const { data, isLoading, isError } = useExperiments({ page, pageSize: PAGE_SIZE, status });

  const items = data?.items ?? [];
  const totalPages = data?.totalPages ?? 0;

  return (
    <div className="space-y-6">
      <PageHeader
        title="Experimentos"
        description="Ensaios in vitro (viabilidade celular e óxido nítrico) — desenho da placa, importação de leitura, cálculo e exportação."
        actions={
          <Button onClick={() => setCreating(true)}>
            <Plus className="size-4" />
            Novo experimento
          </Button>
        }
      />

      <div
        role="tablist"
        aria-label="Filtrar por status"
        className="inline-flex flex-wrap gap-1 rounded-lg bg-muted p-1"
      >
        {STATUS_FILTERS.map(({ value, label }) => (
          <button
            key={value || 'all'}
            role="tab"
            type="button"
            aria-selected={status === value}
            onClick={() => {
              setStatus(value);
              setPage(1);
            }}
            className={cn(
              'rounded-md px-4 py-1.5 text-sm font-medium transition-colors',
              status === value
                ? 'bg-card text-foreground shadow-sm'
                : 'text-muted-foreground hover:text-foreground',
            )}
          >
            {label}
          </button>
        ))}
      </div>

      <div className="rounded-lg border bg-card">
        {isLoading ? (
          <div className="flex items-center justify-center gap-2 p-10 text-sm text-muted-foreground">
            <Loader2 className="size-4 animate-spin" />
            Carregando experimentos…
          </div>
        ) : isError ? (
          <p className="p-10 text-center text-sm text-destructive">
            Não foi possível carregar os experimentos.
          </p>
        ) : items.length === 0 ? (
          <p className="p-10 text-center text-sm text-muted-foreground">
            Nenhum experimento ainda. Crie o primeiro com “Novo experimento”.
          </p>
        ) : (
          <table className="w-full text-sm">
            <thead className="border-b text-left text-xs uppercase tracking-wide text-muted-foreground">
              <tr>
                <th className="px-4 py-3 font-medium">Título</th>
                <th className="px-4 py-3 font-medium">Tipo</th>
                <th className="px-4 py-3 font-medium">Status</th>
                <th className="px-4 py-3 font-medium">Calculado</th>
                <th className="px-4 py-3 font-medium">Criado em</th>
                <th className="px-4 py-3 font-medium">Por</th>
              </tr>
            </thead>
            <tbody>
              {items.map((experiment) => {
                const presentation =
                  experimentStatusPresentation[experiment.status as ExperimentStatus];
                return (
                  <tr
                    key={experiment.id}
                    onClick={() => navigate(`/experiments/${experiment.id}`)}
                    className="cursor-pointer border-b last:border-0 transition-colors hover:bg-accent/50"
                  >
                    <td className="px-4 py-3 font-medium">{experiment.title}</td>
                    <td className="px-4 py-3 text-muted-foreground">
                      {typePresentation(experiment.type).label}
                    </td>
                    <td className="px-4 py-3">
                      <Badge variant={presentation?.variant ?? 'muted'}>
                        {presentation?.label ?? experiment.status}
                      </Badge>
                    </td>
                    <td className="px-4 py-3 text-muted-foreground">
                      {experiment.isCalculated ? 'Sim' : '—'}
                    </td>
                    <td className="px-4 py-3 text-muted-foreground">
                      {formatDate(experiment.createdAtUtc)}
                    </td>
                    <td className="px-4 py-3 text-muted-foreground">{experiment.createdBy}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        )}
      </div>

      {totalPages > 1 && (
        <div className="flex items-center justify-end gap-2">
          <Button
            variant="outline"
            size="sm"
            disabled={page <= 1}
            onClick={() => setPage((p) => Math.max(1, p - 1))}
          >
            Anterior
          </Button>
          <span className="text-sm text-muted-foreground">
            Página {page} de {totalPages}
          </span>
          <Button
            variant="outline"
            size="sm"
            disabled={page >= totalPages}
            onClick={() => setPage((p) => p + 1)}
          >
            Próxima
          </Button>
        </div>
      )}

      {creating && (
        <CreateExperimentModal
          onClose={() => setCreating(false)}
          onCreated={(id) => {
            setCreating(false);
            navigate(`/experiments/${id}`);
          }}
        />
      )}
    </div>
  );
}
