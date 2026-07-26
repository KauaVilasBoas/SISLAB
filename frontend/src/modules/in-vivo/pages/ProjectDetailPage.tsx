import { useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import {
  ArrowLeft,
  Box,
  CalendarClock,
  FlaskConical,
  FlaskRound,
  Loader2,
  Play,
  Plus,
} from 'lucide-react';
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
  AddCageModal,
  AddGroupModal,
  AssignAnimalToGroupModal,
} from '@/modules/in-vivo/components/ProjectDesignModals';
import { LaunchBehavioralModal } from '@/modules/in-vivo/components/LaunchBehavioralModal';
import { GenerateScheduleModal } from '@/modules/in-vivo/components/GenerateScheduleModal';
import { PrepareSolutionModal } from '@/modules/in-vivo/components/PrepareSolutionModal';
import { SolutionPreparationsPanel } from '@/modules/in-vivo/components/SolutionPreparationsPanel';
import { BatchModelPanel } from '@/modules/in-vivo/components/BatchModelPanel';
import { BaselinePanel } from '@/modules/in-vivo/components/BaselinePanel';
import { PhysiologicalReadingsPanel } from '@/modules/in-vivo/components/PhysiologicalReadingsPanel';
import { AnimalSelectionPanel } from '@/modules/in-vivo/components/AnimalSelectionPanel';
import { CollectionPlanPanel } from '@/modules/in-vivo/components/CollectionPlanPanel';
import {
  animalSexLabel,
  batchStatusPresentation,
  formatAmount,
  projectStatusPresentation,
} from '@/modules/in-vivo/presentation';
import type {
  AnimalDetail,
  BatchDetail,
  BatchStatus,
  CageDetail,
  GroupDetail,
  ProjectStatus,
} from '@/modules/in-vivo/types';

/**
 * Project delineation detail (cards [E11] #73, SISLAB-03): the Project → Batch → Cage → Animal tree with
 * permission-gated actions. The flow is caixa → animal → (after basal) atribuir a grupo: animals enter a
 * cage unassigned, are measured, then assigned/moved to a dose group. Assignment is frozen once the leva
 * starts. Dose groups are shown as the assignable arms; a baseline panel summarizes readings by cage/group.
 * A thin orchestration shell around the detail query and the design modals.
 */
export function ProjectDetailPage() {
  const { projectId = '' } = useParams();
  const navigate = useNavigate();
  const toast = useToast();

  const { data: project, isLoading, isError } = useProject(projectId);
  const startBatch = useStartBatch(projectId);

  const [addingBatch, setAddingBatch] = useState(false);
  const [addingGroupTo, setAddingGroupTo] = useState<string | null>(null);
  const [addingCageTo, setAddingCageTo] = useState<string | null>(null);
  const [addingAnimalTo, setAddingAnimalTo] = useState<{
    batchId: string;
    cage: CageDetail;
  } | null>(null);
  const [assigningAnimal, setAssigningAnimal] = useState<{
    batchId: string;
    animal: AnimalDetail;
    groups: GroupDetail[];
  } | null>(null);
  const [launchingBatch, setLaunchingBatch] = useState<string | null>(null);
  const [schedulingBatch, setSchedulingBatch] = useState<BatchDetail | null>(null);
  const [preparingSolution, setPreparingSolution] = useState<{
    batch: BatchDetail;
    group: GroupDetail;
  } | null>(null);

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
            const animalCount = batch.cages.reduce(
              (total, cage) => total + cage.animals.length,
              0,
            );
            const unassignedCount = batch.cages.reduce(
              (total, cage) =>
                total + cage.animals.filter((animal) => animal.groupId === null).length,
              0,
            );
            return (
              <div key={batch.id} className="rounded-lg border bg-card">
                <div className="flex flex-wrap items-center justify-between gap-3 border-b p-4">
                  <div className="flex flex-wrap items-center gap-3">
                    <span className="font-semibold">{batch.name}</span>
                    <Badge variant={batchPresentation?.variant ?? 'muted'}>
                      {batchPresentation?.label ?? batch.status}
                    </Badge>
                    <span className="text-xs text-muted-foreground">
                      v{batch.designVersion}
                    </span>
                    <span className="text-xs text-muted-foreground">
                      {batch.cages.length} caixa(s) · {animalCount} animal(is)
                    </span>
                    {unassignedCount > 0 && (
                      <Badge variant="secondary">{unassignedCount} sem grupo</Badge>
                    )}
                  </div>
                  <div className="flex flex-wrap items-center gap-2">
                    {isPlanned && (
                      <RequirePermission code={Permissions.projects.addCage}>
                        <Button
                          variant="outline"
                          size="sm"
                          onClick={() => setAddingCageTo(batch.id)}
                        >
                          <Plus className="size-4" />
                          Caixa
                        </Button>
                      </RequirePermission>
                    )}
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
                    {/* Schedule generation needs the induction cadence — only offered once a model is bound (SISLAB-10). */}
                    {isRunning && batch.experimentalModelId && (
                      <RequirePermission code={Permissions.scheduling.generate}>
                        <Button
                          variant="outline"
                          size="sm"
                          onClick={() => setSchedulingBatch(batch)}
                        >
                          <CalendarClock className="size-4" />
                          Gerar cronograma
                        </Button>
                      </RequirePermission>
                    )}
                  </div>
                </div>

                <BatchModelPanel projectId={project.id} batch={batch} />

                {/* Dose groups (assignable arms) — with the per-group solution-preparation action. */}
                <GroupsSection
                  batch={batch}
                  onPrepare={(group) => setPreparingSolution({ batch, group })}
                />

                {/* Cages (caixas) with the animals they house (SISLAB-03). */}
                <CagesSection
                  batch={batch}
                  isPlanned={isPlanned}
                  onAddAnimal={(cage) => setAddingAnimalTo({ batchId: batch.id, cage })}
                  onAssign={(animal) =>
                    setAssigningAnimal({
                      batchId: batch.id,
                      animal,
                      groups: batch.groups,
                    })
                  }
                />

                {/* Physiological readings (glicemia/peso, …) per animal/timepoint, filtered by the batch model (SISLAB-02). */}
                <PhysiologicalReadingsPanel projectId={project.id} batch={batch} />

                {/* Apply the inclusion criteria and review included/excluded animals (SISLAB-02). */}
                <AnimalSelectionPanel projectId={project.id} batch={batch} />

                {/* Collection plan: sample→analysis matrix, collection roles and the derived status board (SISLAB-08). */}
                <CollectionPlanPanel projectId={project.id} batchId={batch.id} />

                {/* Baseline (mean/min/max) by cage and by group — the Prism pre/post-randomization views. */}
                <div className="border-t p-4">
                  <BaselinePanel projectId={project.id} batchId={batch.id} />
                </div>
              </div>
            );
          })}
        </div>
      )}

      <SolutionPreparationsPanel projectId={project.id} />

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
      {addingCageTo && (
        <AddCageModal
          projectId={project.id}
          batchId={addingCageTo}
          onClose={() => setAddingCageTo(null)}
        />
      )}
      {addingAnimalTo && (
        <AddAnimalModal
          projectId={project.id}
          batchId={addingAnimalTo.batchId}
          cageId={addingAnimalTo.cage.id}
          cageName={addingAnimalTo.cage.name}
          onClose={() => setAddingAnimalTo(null)}
        />
      )}
      {assigningAnimal && (
        <AssignAnimalToGroupModal
          projectId={project.id}
          batchId={assigningAnimal.batchId}
          animalId={assigningAnimal.animal.id}
          animalIdentifier={assigningAnimal.animal.identifier}
          currentGroupId={assigningAnimal.animal.groupId}
          groups={assigningAnimal.groups}
          onClose={() => setAssigningAnimal(null)}
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
      {preparingSolution && (
        <PrepareSolutionModal
          projectId={project.id}
          batch={preparingSolution.batch}
          group={preparingSolution.group}
          onClose={() => setPreparingSolution(null)}
        />
      )}
      {schedulingBatch && (
        <GenerateScheduleModal
          experimentId={schedulingBatch.id}
          batch={schedulingBatch}
          onClose={() => setSchedulingBatch(null)}
        />
      )}
    </div>
  );
}

/** The batch's dose groups (assignable treatment arms) with a per-group solution-preparation action. */
function GroupsSection({
  batch,
  onPrepare,
}: {
  batch: BatchDetail;
  onPrepare: (group: GroupDetail) => void;
}) {
  const assignedCountByGroup = new Map<string, number>();
  for (const cage of batch.cages) {
    for (const animal of cage.animals) {
      if (animal.groupId) {
        assignedCountByGroup.set(
          animal.groupId,
          (assignedCountByGroup.get(animal.groupId) ?? 0) + 1,
        );
      }
    }
  }

  return (
    <div className="border-t p-4">
      <p className="mb-3 text-xs font-medium uppercase tracking-wide text-muted-foreground">
        Grupos (dose)
      </p>
      {batch.groups.length === 0 ? (
        <p className="rounded-md border border-dashed p-4 text-center text-sm text-muted-foreground">
          Nenhum grupo. Adicione braços de dose para atribuir os animais após o basal.
        </p>
      ) : (
        <div className="flex flex-wrap gap-2">
          {batch.groups.map((group) => (
            <div
              key={group.id}
              className="flex items-center gap-2 rounded-lg border bg-muted/30 px-3 py-2"
            >
              <div className="min-w-0">
                <span className="block truncate text-sm font-medium">{group.name}</span>
                <span className="block text-xs text-muted-foreground">
                  {formatAmount(group.doseAmount, group.doseUnit)} ·{' '}
                  {assignedCountByGroup.get(group.id) ?? 0} animal(is)
                </span>
              </div>
              <RequirePermission code={Permissions.projects.prepareGroupSolution}>
                <Button variant="ghost" size="sm" onClick={() => onPrepare(group)}>
                  <FlaskRound className="size-4" />
                  Preparar
                </Button>
              </RequirePermission>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

/** The batch's cages with the animals they house; each animal can be assigned/moved to a group after basal. */
function CagesSection({
  batch,
  isPlanned,
  onAddAnimal,
  onAssign,
}: {
  batch: BatchDetail;
  isPlanned: boolean;
  onAddAnimal: (cage: CageDetail) => void;
  onAssign: (animal: AnimalDetail) => void;
}) {
  const groupNameById = new Map(batch.groups.map((group) => [group.id, group.name]));

  return (
    <div className="border-t p-4">
      <p className="mb-3 text-xs font-medium uppercase tracking-wide text-muted-foreground">
        Caixas
      </p>
      {batch.cages.length === 0 ? (
        <p className="rounded-md border border-dashed p-4 text-center text-sm text-muted-foreground">
          Nenhuma caixa. Adicione caixas e aloje os animais antes de randomizar.
        </p>
      ) : (
        <div className="grid gap-3 md:grid-cols-2">
          {batch.cages.map((cage) => (
            <div key={cage.id} className="rounded-lg border">
              <div className="flex items-center justify-between gap-2 border-b bg-muted/20 px-3 py-2">
                <div className="flex items-center gap-2">
                  <Box className="size-4 text-muted-foreground" />
                  <span className="text-sm font-medium">{cage.name}</span>
                  <span className="text-xs text-muted-foreground">
                    {cage.animals.length}
                    {cage.capacity != null ? `/${cage.capacity}` : ''} animal(is)
                  </span>
                </div>
                {isPlanned && (
                  <RequirePermission code={Permissions.projects.addAnimal}>
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => onAddAnimal(cage)}
                      disabled={
                        cage.capacity != null && cage.animals.length >= cage.capacity
                      }
                    >
                      <Plus className="size-4" />
                      Animal
                    </Button>
                  </RequirePermission>
                )}
              </div>
              {cage.animals.length === 0 ? (
                <p className="px-3 py-4 text-center text-xs text-muted-foreground">
                  Caixa vazia.
                </p>
              ) : (
                <ul className="divide-y">
                  {cage.animals.map((animal) => (
                    <li
                      key={animal.id}
                      className="flex items-center justify-between gap-2 px-3 py-2"
                    >
                      <div className="flex min-w-0 flex-wrap items-center gap-2">
                        <span className="text-sm font-medium">{animal.identifier}</span>
                        <span className="text-xs text-muted-foreground">
                          {animalSexLabel[animal.sex]}
                          {animal.weightGrams != null ? ` · ${animal.weightGrams} g` : ''}
                        </span>
                        {animal.groupId ? (
                          <Badge variant="default">
                            {groupNameById.get(animal.groupId) ?? 'Grupo'}
                          </Badge>
                        ) : (
                          <Badge variant="secondary">Sem grupo</Badge>
                        )}
                      </div>
                      {isPlanned && (
                        <RequirePermission
                          code={Permissions.projects.assignAnimalToGroup}
                        >
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={() => onAssign(animal)}
                          >
                            {animal.groupId ? 'Mover' : 'Atribuir'}
                          </Button>
                        </RequirePermission>
                      )}
                    </li>
                  ))}
                </ul>
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
