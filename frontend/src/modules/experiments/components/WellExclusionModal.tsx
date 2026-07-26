import { useState, type FormEvent } from 'react';
import { Loader2, Undo2 } from 'lucide-react';
import { Modal } from '@/shared/components/ui/modal';
import { Button } from '@/shared/components/ui/button';
import { Label } from '@/shared/components/ui/label';
import { useToast } from '@/shared/components/ui/toast';
import type { ApiError } from '@/shared/types/api';
import { useHasPermission } from '@/modules/auth/PermissionsProvider';
import { Permissions } from '@/modules/auth/permissions';
import {
  useExcludeWell,
  useIncludeWell,
} from '@/modules/experiments/api/experiments.queries';
import type { PlateWellResult } from '@/modules/experiments/types';

interface WellExclusionModalProps {
  experimentId: string;
  /** The well the operator clicked on the grid. */
  well: PlateWellResult;
  onClose: () => void;
}

/**
 * Outlier-exclusion dialog for a single plate well (SISLAB-06). Opened from the plate grid while the
 * experiment is NOT yet calculated (the caller only wires the grid's onWellClick in that window, and the
 * backend rejects a post-freeze exclusion with 409 as a second line of defense).
 *
 * A designed well is either included — the operator gives a reason and excludes it — or already excluded —
 * the dialog shows the recorded reason/author and offers to re-include it. Both actions invalidate the
 * plate design/grid via the mutation hooks, so the cell re-renders immediately.
 */
export function WellExclusionModal({
  experimentId,
  well,
  onClose,
}: WellExclusionModalProps) {
  const toast = useToast();
  const coordinate = `${well.row}${well.column}`;
  const canExclude = useHasPermission(Permissions.experiments.excludeWell);
  const canInclude = useHasPermission(Permissions.experiments.includeWell);

  const exclude = useExcludeWell(experimentId);
  const include = useIncludeWell(experimentId);
  const [reason, setReason] = useState('');

  async function handleExclude(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    try {
      await exclude.mutateAsync({ coordinate, body: { reason: reason.trim() } });
      toast('success', `Poço ${coordinate} excluído do cálculo.`);
      onClose();
    } catch (err) {
      toast('error', (err as ApiError)?.message ?? 'Não foi possível excluir o poço.');
    }
  }

  async function handleInclude() {
    try {
      await include.mutateAsync(coordinate);
      toast('success', `Poço ${coordinate} reincluído no cálculo.`);
      onClose();
    } catch (err) {
      toast('error', (err as ApiError)?.message ?? 'Não foi possível reincluir o poço.');
    }
  }

  if (well.isExcluded) {
    return (
      <Modal
        open
        onClose={onClose}
        title={`Poço ${coordinate} excluído`}
        description="Este poço foi marcado como outlier e não entra nas médias, na curva nem no resultado."
        footer={
          <>
            <Button variant="outline" onClick={onClose} disabled={include.isPending}>
              Fechar
            </Button>
            {canInclude && (
              <Button onClick={() => void handleInclude()} disabled={include.isPending}>
                {include.isPending ? (
                  <Loader2 className="size-4 animate-spin" />
                ) : (
                  <Undo2 className="size-4" />
                )}
                Reincluir poço
              </Button>
            )}
          </>
        }
      >
        <dl className="space-y-3 text-sm">
          <div>
            <dt className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
              Motivo
            </dt>
            <dd className="mt-0.5">{well.exclusionReason ?? '—'}</dd>
          </div>
          <div>
            <dt className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
              Excluído por
            </dt>
            <dd className="mt-0.5">{well.excludedBy ?? '—'}</dd>
          </div>
        </dl>
      </Modal>
    );
  }

  return (
    <Modal
      open
      onClose={onClose}
      title={`Excluir poço ${coordinate}`}
      description="Marque este poço como outlier antes do cálculo. Ele será ignorado nas médias, na curva-padrão e nos resultados. A decisão fica registrada com o motivo e o autor."
      footer={
        <>
          <Button variant="outline" onClick={onClose} disabled={exclude.isPending}>
            Cancelar
          </Button>
          <Button
            type="submit"
            form="exclude-well-form"
            variant="destructive"
            disabled={!canExclude || exclude.isPending || reason.trim() === ''}
          >
            {exclude.isPending && <Loader2 className="size-4 animate-spin" />}
            Excluir poço
          </Button>
        </>
      }
    >
      {!canExclude ? (
        <p className="text-sm text-muted-foreground">
          Você não tem permissão para excluir poços da placa.
        </p>
      ) : (
        <form
          id="exclude-well-form"
          className="space-y-1.5"
          onSubmit={handleExclude}
          noValidate
        >
          <Label htmlFor="exclusion-reason">Motivo da exclusão</Label>
          <textarea
            id="exclusion-reason"
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            rows={3}
            maxLength={500}
            autoFocus
            placeholder="Ex.: réplica muito fora do padrão das demais (absorbância discrepante)."
            className="flex w-full rounded-md border border-input bg-transparent px-3 py-2 text-sm shadow-sm placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
          />
          <p className="text-xs text-muted-foreground">{reason.trim().length}/500</p>
        </form>
      )}
    </Modal>
  );
}
