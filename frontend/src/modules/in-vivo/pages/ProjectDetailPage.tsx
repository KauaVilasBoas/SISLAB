import { useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { ArrowLeft, Loader2, Plus, Play, FlaskConical } from 'lucide-react';
import { PageHeader } from '@/shared/components/PageHeader';
import { Button } from '@/shared/components/ui/button';
import { Badge } from '@/shared/components/ui/badge';
import { useToast } from '@/shared/components/ui/toast';
import { RequirePermission } from '@/modules/auth/PermissionsProvider';
import { Permissions } from '@/modules/auth/permissions';
import type { ApiError } from '@/shared/types/api';
import { useProject, useStartBatch } from '@/modules/in-vivo/api/projects.queries';
import {
  AddAnimalModal,
  AddBatchModal,
  AddGroupModal,
} from '@/modules/in-vivo/components/ProjectDesignModals';
import { LaunchBehavioralModal } from '@/modules/in-vivo/components/LaunchBehavioralModal';
import {
  animalSexLabel,
  batchStatusPresentation,
  formatAmount,
  projectStatusPresentation,
} from '@/modules/in-vivo/presentation';
import type { BatchStatus, ProjectStatus } from '@/modules/in-vivo/types';

/**
 * Project delineation detail (card [E11] #73): the Project → Batch → Group → Animal tree with permission-gated
 * actions to grow the design (add batch / group / animal), start a batch (freezes its design) and launch a
 * behavioural experiment against a running batch. A thin orchestration shell around the detail query and modals.
 */
export function ProjectDetailPage() {
  const { projectId = '' } = useParams();
  const navigate = useNavigate();
  const toast = useToast();

  const { data: project, isLoading, isError } = useProject(projectId);
  const startBatch = useStartBatch(projectId);

  const [addingBatch, setAddingBatch] = useState(false);
  const [addingGroupTo, setAddingGroupTo] = useState<string | null>(null);
  const [addingAnimalTo, setAddingAnimalTo] = useState<{
    batchId: string;
    groupId: string;
  } | null>(null);
  const [launchingBatch, setLaunchingBatch] = useState<string | null>(null);

  async function handleStartBatch(batchId: string) {
    try {
      await startBatch.mutateAsync(batchId);
      toast('success', 'Leva iniciada — desenho congelado.');
    } catch (err) {
      toast('error', (err as ApiError)?.message ?? 'Não foi possível iniciar a leva.');
    }
  }

  if (isLoading) {
    return (
      <div className="flex items-center justify-center gap-2 p-16 text-sm text-muted-foreground">
        <Loader2 className="size-4 animate-spin" />
        Carregando projeto…
      </div>
    );
  }

  if (isError || !project) {
    return (
      <div className="space-y-4">
        <Button
          variant="ghost"
          size="sm"
          onClick={() => navigate('/experiments/in-vivo/projects')}
        >
          <ArrowLeft className="size-4" />
          Voltar
        </Button>
        <p className="p-10 text-center text-sm text-destructive">
          Não foi possível carregar o projeto.
        </p>
      </div>
    );
  }

  const statusPresentation = projectStatusPresentation[project.status as ProjectStatus];

  return (
    <div className="space-y-6">
      <Button
        variant="ghost"
        size="sm"
        onClick={() => navigate('/experiments/in-vivo/projects')}
      >
        <ArrowLeft className="size-4" />
        Projetos
      </Button>

      <PageHeader
        title={project.name}
        description={`${project.species} · versão de desenho v${project.currentDesignVersion}`}
        actions={
          <div className="flex items-center gap-2">
            <Badge variant={statusPresentation?.variant ?? 'muted'}>
              {statusPresentation?.label ?? project.status}
            </Badge>
            <RequirePermission code={Permissions.projects.addBatch}>
              <Button onClick={() => setAddingBatch(true)}>
                <Plus className="size-4" />
                Nova leva
              </Button>
            </RequirePermission>
          </div>
        }
      />

      {project.description && (
        <p className="text-sm text-muted-foreground">{project.description}</p>
      )}

      {project.batches.length === 0 ? (
        <p className="rounded-lg border bg-card p-10 text-center text-sm text-muted-foreground">
          Nenhuma leva ainda. Adicione a primeira com “Nova leva”.
        </p>
      ) : (
        <div className="space-y-5">
          {project.batches.map((batch) => {
            const batchPresentation =
              batchStatusPresentation[batch.status as BatchStatus];
            const isRunning = batch.status === 'Running';
            const isPlanned = batch.status === 'Planned';
            return (
              <div key={batch.id} className="rounded-lg border bg-card">
                <div className="flex flex-wrap items-center justify-between gap-3 border-b p-4">
                  <div className="flex items-center gap-3">
                    <span className="font-semibold">{batch.name}</span>
                    <Badge variant={batchPresentation?.variant ?? 'muted'}>
                      {batchPresentation?.label ?? batch.status}
                    </Badge>
                    <span className="text-xs text-muted-foreground">
                      v{batch.designVersion}
                    </span>
                  </div>
                  <div className="flex items-center gap-2">
                    {isPlanned && (
                      <RequirePermission code={Permissions.projects.addGroup}>
                        <Button
                          variant="outline"
                          size="sm"
                          onClick={() => setAddingGroupTo(batch.id)}
                        >
                          <Plus className="size-4" />
                          Grupo
                        </Button>
                      </RequirePermission>
                    )}
                    {isPlanned && batch.groups.length > 0 && (
                      <RequirePermission code={Permissions.projects.startBatch}>
                        <Button
                          size="sm"
                          disabled={startBatch.isPending}
                          onClick={() => handleStartBatch(batch.id)}
                        >
                          <Play className="size-4" />
                          Iniciar leva
                        </Button>
                      </RequirePermission>
                    )}
                    {isRunning && (
                      <RequirePermission code={Permissions.experiments.createBehavioral}>
                        <Button
                          variant="outline"
                          size="sm"
                          onClick={() => setLaunchingBatch(batch.id)}
                        >
                          <FlaskConical className="size-4" />
                          Lançar teste
                        </Button>
                      </RequirePermission>
                    )}
                  </div>
                </div>

                {batch.groups.length === 0 ? (
                  <p className="p-6 text-center text-sm text-muted-foreground">
                    Nenhum grupo. Adicione braços de dose antes de iniciar a leva.
                  </p>
                ) : (
                  <div className="divide-y">
                    {batch.groups.map((group) => (
                      <div key={group.id} className="p-4">
                        <div className="mb-2 flex flex-wrap items-center justify-between gap-2">
                          <div className="flex items-center gap-2">
                            <span className="font-medium">{group.name}</span>
                            <Badge variant="muted">
                              {formatAmount(group.doseAmount, group.doseUnit)}
                            </Badge>
                            <span className="text-xs text-muted-foreground">
                              {group.animals.length} animal(is)
                            </span>
                          </div>
                          {isPlanned && (
                            <RequirePermission code={Permissions.projects.addAnimal}>
                              <Button
                                variant="ghost"
                                size="sm"
                                onClick={() =>
                                  setAddingAnimalTo({
                                    batchId: batch.id,
                                    groupId: group.id,
                                  })
                                }
                              >
                                <Plus className="size-4" />
                                Animal
                              </Button>
                            </RequirePermission>
                          )}
                        </div>
                        {group.animals.length > 0 && (
                          <div className="flex flex-wrap gap-2">
                            {group.animals.map((animal) => (
                              <span
                                key={animal.id}
                                className="inline-flex items-center gap-1.5 rounded-md border bg-muted/40 px-2.5 py-1 text-xs"
                              >
                                <span className="font-medium">{animal.identifier}</span>
                                <span className="text-muted-foreground">
                                  {animalSexLabel[animal.sex]}
                                  {animal.weightGrams != null
                                    ? ` · ${animal.weightGrams} g`
                                    : ''}
                                </span>
                              </span>
                            ))}
                          </div>
                        )}
                      </div>
                    ))}
                  </div>
                )}
              </div>
            );
          })}
        </div>
      )}

      {addingBatch && (
        <AddBatchModal projectId={project.id} onClose={() => setAddingBatch(false)} />
      )}
      {addingGroupTo && (
        <AddGroupModal
          projectId={project.id}
          batchId={addingGroupTo}
          onClose={() => setAddingGroupTo(null)}
        />
      )}
      {addingAnimalTo && (
        <AddAnimalModal
          projectId={project.id}
          batchId={addingAnimalTo.batchId}
          groupId={addingAnimalTo.groupId}
          onClose={() => setAddingAnimalTo(null)}
        />
      )}
      {launchingBatch && (
        <LaunchBehavioralModal
          projectId={project.id}
          batchId={launchingBatch}
          onClose={() => setLaunchingBatch(null)}
          onLaunched={(experimentId) => {
            setLaunchingBatch(null);
            navigate(`/experiments/${experimentId}`);
          }}
        />
      )}
    </div>
  );
}
