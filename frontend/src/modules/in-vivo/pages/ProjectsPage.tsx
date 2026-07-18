import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Plus, Loader2 } from 'lucide-react';
import { PageHeader } from '@/shared/components/PageHeader';
import { Button } from '@/shared/components/ui/button';
import { Badge } from '@/shared/components/ui/badge';
import { cn } from '@/shared/lib/utils';
import { RequirePermission } from '@/modules/auth/PermissionsProvider';
import { Permissions } from '@/modules/auth/permissions';
import { useProjects } from '@/modules/in-vivo/api/projects.queries';
import { CreateProjectModal } from '@/modules/in-vivo/components/CreateProjectModal';
import { projectStatusPresentation } from '@/modules/in-vivo/presentation';
import type { ProjectStatus } from '@/modules/in-vivo/types';

const PAGE_SIZE = 20;

const STATUS_FILTERS: { value: string; label: string }[] = [
  { value: '', label: 'Todos' },
  { value: 'Draft', label: 'Rascunho' },
  { value: 'Active', label: 'Ativo' },
  { value: 'Closed', label: 'Encerrado' },
];

/**
 * In vivo projects list (card [E11] #73). A paginated table of the active company's experimental designs with a
 * status filter and a permission-gated "New project" action; each row navigates to the delineation detail.
 */
export function ProjectsPage() {
  const navigate = useNavigate();
  const [page, setPage] = useState(1);
  const [status, setStatus] = useState('');
  const [creating, setCreating] = useState(false);

  const { data, isLoading, isError } = useProjects({ page, pageSize: PAGE_SIZE, status });

  const items = data?.items ?? [];
  const totalPages = data?.totalPages ?? 0;

  return (
    <div className="space-y-6">
      <PageHeader
        title="Projetos in vivo"
        description="Delineamento experimental — Projeto → Leva → Grupo (dose) → Animal."
        actions={
          <RequirePermission code={Permissions.projects.create}>
            <Button onClick={() => setCreating(true)}>
              <Plus className="size-4" />
              Novo projeto
            </Button>
          </RequirePermission>
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
            Carregando projetos…
          </div>
        ) : isError ? (
          <p className="p-10 text-center text-sm text-destructive">
            Não foi possível carregar os projetos.
          </p>
        ) : items.length === 0 ? (
          <p className="p-10 text-center text-sm text-muted-foreground">
            Nenhum projeto ainda. Crie o primeiro com “Novo projeto”.
          </p>
        ) : (
          <table className="w-full text-sm">
            <thead className="border-b text-left text-xs uppercase tracking-wide text-muted-foreground">
              <tr>
                <th className="px-4 py-3 font-medium">Nome</th>
                <th className="px-4 py-3 font-medium">Espécie</th>
                <th className="px-4 py-3 font-medium">Status</th>
                <th className="px-4 py-3 font-medium">Versão</th>
                <th className="px-4 py-3 font-medium">Levas</th>
                <th className="px-4 py-3 font-medium">Animais</th>
              </tr>
            </thead>
            <tbody>
              {items.map((project) => {
                const presentation =
                  projectStatusPresentation[project.status as ProjectStatus];
                return (
                  <tr
                    key={project.id}
                    onClick={() =>
                      navigate(`/experiments/in-vivo/projects/${project.id}`)
                    }
                    className="cursor-pointer border-b last:border-0 transition-colors hover:bg-accent/50"
                  >
                    <td className="px-4 py-3 font-medium">{project.name}</td>
                    <td className="px-4 py-3 text-muted-foreground">{project.species}</td>
                    <td className="px-4 py-3">
                      <Badge variant={presentation?.variant ?? 'muted'}>
                        {presentation?.label ?? project.status}
                      </Badge>
                    </td>
                    <td className="px-4 py-3 text-muted-foreground">
                      v{project.designVersion}
                    </td>
                    <td className="px-4 py-3 text-muted-foreground">
                      {project.batchCount}
                    </td>
                    <td className="px-4 py-3 text-muted-foreground">
                      {project.animalCount}
                    </td>
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
        <CreateProjectModal
          onClose={() => setCreating(false)}
          onCreated={(id) => {
            setCreating(false);
            navigate(`/experiments/in-vivo/projects/${id}`);
          }}
        />
      )}
    </div>
  );
}
