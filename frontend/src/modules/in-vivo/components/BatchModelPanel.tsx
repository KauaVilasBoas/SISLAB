import { useState } from 'react';
import { Loader2, Microscope, Pencil } from 'lucide-react';
import { Button } from '@/shared/components/ui/button';
import { Badge } from '@/shared/components/ui/badge';
import { Modal } from '@/shared/components/ui/modal';
import { Label } from '@/shared/components/ui/label';
import { SingleSelect } from '@/shared/components/ui/single-select';
import { useToast } from '@/shared/components/ui/toast';
import type { ApiError } from '@/shared/types/api';
import { RequirePermission } from '@/modules/auth/PermissionsProvider';
import { Permissions } from '@/modules/auth/permissions';
import {
  useExperimentalModel,
  useExperimentalModels,
} from '@/modules/configuration/api/configuration.queries';
import { useBindBatchModel } from '@/modules/in-vivo/api/projects.queries';
import type { BatchDetail } from '@/modules/in-vivo/types';

/** Human label for each applicable-parameter code (SISLAB-04); falls back to the raw code. */
const parameterLabel: Record<string, string> = {
  glicemia: 'Glicemia',
  rotarod: 'Rotarod',
  peso: 'Peso',
};

interface BatchModelPanelProps {
  projectId: string;
  batch: BatchDetail;
}

/**
 * Experimental-model binding + summary for one batch (SISLAB-04 #2/#3). Shows the batch's bound model (its
 * timepoints and applicable parameters) and, while the batch is still Planned (design open), offers to bind or
 * change it. Once the batch is Running the design is frozen, so the action is hidden — mirroring the backend,
 * which rejects binding a started batch.
 *
 * The rendered timepoints/parameters are the SISLAB-02 filtering source: the readings/timepoint screens (yet to
 * come) should offer only `model.timepoints` and only the fields whose code is in `model.parameters` — e.g. hide
 * the glicemia field when "glicemia" is not listed. See {@link useExperimentalModel}.
 */
export function BatchModelPanel({ projectId, batch }: BatchModelPanelProps) {
  const [editing, setEditing] = useState(false);
  const model = useExperimentalModel(batch.experimentalModelId);
  const canChange = batch.status === 'Planned';

  return (
    <div className="border-t bg-muted/30 px-4 py-3">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex items-center gap-2 text-sm">
          <Microscope className="size-4 text-muted-foreground" />
          <span className="text-muted-foreground">Modelo experimental:</span>
          {batch.experimentalModelId ? (
            model.isLoading ? (
              <span className="text-muted-foreground">carregando…</span>
            ) : model.data ? (
              <span className="font-medium">{model.data.name}</span>
            ) : (
              <span className="text-muted-foreground">modelo indisponível</span>
            )
          ) : (
            <span className="italic text-muted-foreground">nenhum vinculado</span>
          )}
        </div>
        {canChange && (
          <RequirePermission code={Permissions.projects.bindBatchModel}>
            <Button variant="ghost" size="sm" onClick={() => setEditing(true)}>
              <Pencil className="size-4" />
              {batch.experimentalModelId ? 'Trocar modelo' : 'Vincular modelo'}
            </Button>
          </RequirePermission>
        )}
      </div>

      {batch.experimentalModelId && model.data && (
        <div className="mt-3 grid gap-3 sm:grid-cols-2">
          <div>
            <p className="mb-1 text-xs font-medium uppercase tracking-wide text-muted-foreground">
              Timepoints
            </p>
            <div className="flex flex-wrap gap-1.5">
              {model.data.timepoints.length > 0 ? (
                model.data.timepoints.map((label) => (
                  <Badge key={label} variant="muted">
                    {label}
                  </Badge>
                ))
              ) : (
                <span className="text-xs text-muted-foreground">—</span>
              )}
            </div>
          </div>
          <div>
            <p className="mb-1 text-xs font-medium uppercase tracking-wide text-muted-foreground">
              Parâmetros aplicáveis
            </p>
            <div className="flex flex-wrap gap-1.5">
              {model.data.parameters.length > 0 ? (
                model.data.parameters.map((code) => (
                  <Badge key={code} variant="secondary">
                    {parameterLabel[code] ?? code}
                  </Badge>
                ))
              ) : (
                <span className="text-xs text-muted-foreground">nenhum</span>
              )}
            </div>
          </div>
        </div>
      )}

      {editing && (
        <BindBatchModelModal
          projectId={projectId}
          batchId={batch.id}
          currentModelId={batch.experimentalModelId}
          onClose={() => setEditing(false)}
        />
      )}
    </div>
  );
}

interface BindBatchModelModalProps {
  projectId: string;
  batchId: string;
  currentModelId: string | null;
  onClose: () => void;
}

function BindBatchModelModal({
  projectId,
  batchId,
  currentModelId,
  onClose,
}: BindBatchModelModalProps) {
  const models = useExperimentalModels();
  const bind = useBindBatchModel(projectId, batchId);
  const toast = useToast();
  const [selected, setSelected] = useState<string | null>(currentModelId);

  const options = (models.data ?? []).map((model) => ({
    value: model.id,
    label: model.name,
    hint: model.description ?? undefined,
  }));

  async function handleConfirm() {
    if (!selected) {
      toast('error', 'Escolha um modelo experimental.');
      return;
    }
    try {
      await bind.mutateAsync({ experimentalModelId: selected });
      toast('success', 'Modelo experimental vinculado à leva.');
      onClose();
    } catch (err) {
      toast('error', (err as ApiError)?.message ?? 'Não foi possível vincular o modelo.');
    }
  }

  return (
    <Modal
      open
      onClose={onClose}
      title="Vincular modelo experimental"
      description="Escolha o protocolo de indução da leva. Só é possível enquanto o desenho está aberto (leva planejada)."
      footer={
        <>
          <Button variant="outline" onClick={onClose} disabled={bind.isPending}>
            Cancelar
          </Button>
          <Button onClick={handleConfirm} disabled={bind.isPending || !selected}>
            {bind.isPending && <Loader2 className="size-4 animate-spin" />}
            Vincular
          </Button>
        </>
      }
    >
      <div className="flex flex-col gap-2">
        <Label>Modelo experimental</Label>
        {models.isLoading ? (
          <div className="flex items-center gap-2 text-sm text-muted-foreground">
            <Loader2 className="size-4 animate-spin" />
            Carregando modelos…
          </div>
        ) : models.isError ? (
          <p className="text-sm text-destructive">Não foi possível carregar os modelos.</p>
        ) : options.length === 0 ? (
          <p className="text-sm text-muted-foreground">
            Nenhum modelo cadastrado. Crie um em Configurações → Modelos experimentais.
          </p>
        ) : (
          <SingleSelect
            label="Modelo experimental"
            placeholder="Selecionar modelo…"
            options={options}
            value={selected}
            onChange={setSelected}
          />
        )}
      </div>
    </Modal>
  );
}
