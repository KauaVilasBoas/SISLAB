import { useState, type FormEvent } from 'react';
import { Loader2 } from 'lucide-react';
import { Modal } from '@/shared/components/ui/modal';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import { Label } from '@/shared/components/ui/label';
import { useToast } from '@/shared/components/ui/toast';
import type { ApiError } from '@/shared/types/api';
import { useDesignPlate } from '@/modules/experiments/api/experiments.queries';
import type { DesignPlateWellRequest, ExperimentType } from '@/modules/experiments/types';

interface DesignPlateModalProps {
  experimentId: string;
  experimentType: string;
  onClose: () => void;
}

const ROWS = ['A', 'B', 'C', 'D', 'E', 'F', 'G', 'H'];

/**
 * Plate-design modal for the in vitro slice (cards [E11] #68 / #72). Rather than a full drag-and-drop plate
 * editor, it lays out a standard column-based template that differs by assay:
 * <ul>
 *   <li><b>Viability:</b> column 1 = blanks, column 2 = controls, one column per sample (each filling 8 rows).</li>
 *   <li><b>Nitric oxide:</b> column 1 = blanks, column 2 = the calibration curve (one Standard well per point at an
 *   ascending nitrite µM), one column per sample.</li>
 * </ul>
 * This is enough to exercise the whole design → import → calculate → export flow; a rich editor can replace it
 * later without touching the backend contract.
 */
export function DesignPlateModal({ experimentId, experimentType, onClose }: DesignPlateModalProps) {
  const toast = useToast();
  const design = useDesignPlate(experimentId);
  const isNitricOxide = (experimentType as ExperimentType) === 'NitricOxide';

  const [blanks, setBlanks] = useState(2);
  const [controls, setControls] = useState(3);
  const [standards, setStandards] = useState(5);
  const [samples, setSamples] = useState(3);

  function buildWells(): DesignPlateWellRequest[] {
    const wells: DesignPlateWellRequest[] = [];
    let column = 1;

    const pushColumn = (
      role: DesignPlateWellRequest['role'],
      count: number,
      concentrationFor?: (rowIndex: number) => number | null,
      sampleIndex?: number,
    ) => {
      for (let i = 0; i < count && i < ROWS.length; i += 1) {
        wells.push({
          row: ROWS[i],
          column,
          role,
          concentrationUm: concentrationFor ? concentrationFor(i) : null,
          sampleId: role === 'Sample' ? `S${(sampleIndex ?? 0) + 1}` : null,
        });
      }
      column += 1;
    };

    pushColumn('Blank', blanks);

    if (isNitricOxide) {
      // Calibration curve: one Standard well per point, ascending nitrite µM (0, 10, 20, ... a simple ramp the
      // operator can adjust after import; the strategy only needs concentration × absorbance pairs).
      pushColumn('Standard', standards, (i) => i * 10);
    } else {
      pushColumn('Control', controls);
    }

    for (let s = 0; s < samples; s += 1) {
      pushColumn('Sample', ROWS.length, () => (isNitricOxide ? null : s + 1), s);
    }

    return wells;
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    try {
      await design.mutateAsync(buildWells());
      toast('success', 'Placa desenhada.');
      onClose();
    } catch (err) {
      toast('error', (err as ApiError)?.message ?? 'Não foi possível desenhar a placa.');
    }
  }

  const columnsUsed = 2 + samples;

  return (
    <Modal
      open
      onClose={onClose}
      title="Desenhar placa"
      description={
        isNitricOxide
          ? 'Modelo por colunas: brancos, curva de calibração (padrões) e uma coluna por amostra (placa 8×12).'
          : 'Modelo por colunas: brancos, controles e uma coluna por amostra (placa 8×12).'
      }
      footer={
        <>
          <Button variant="outline" onClick={onClose} disabled={design.isPending}>
            Cancelar
          </Button>
          <Button
            type="submit"
            form="design-plate-form"
            disabled={design.isPending || columnsUsed > 12}
          >
            {design.isPending && <Loader2 className="size-4 animate-spin" />}
            Aplicar desenho
          </Button>
        </>
      }
    >
      <form id="design-plate-form" className="space-y-4" onSubmit={handleSubmit} noValidate>
        <div className="grid grid-cols-3 gap-3">
          <div className="space-y-1.5">
            <Label htmlFor="blanks">Brancos</Label>
            <Input
              id="blanks"
              type="number"
              min={1}
              max={8}
              value={blanks}
              onChange={(e) => setBlanks(Number(e.target.value))}
            />
          </div>
          {isNitricOxide ? (
            <div className="space-y-1.5">
              <Label htmlFor="standards">Padrões</Label>
              <Input
                id="standards"
                type="number"
                min={2}
                max={8}
                value={standards}
                onChange={(e) => setStandards(Number(e.target.value))}
              />
            </div>
          ) : (
            <div className="space-y-1.5">
              <Label htmlFor="controls">Controles</Label>
              <Input
                id="controls"
                type="number"
                min={1}
                max={8}
                value={controls}
                onChange={(e) => setControls(Number(e.target.value))}
              />
            </div>
          )}
          <div className="space-y-1.5">
            <Label htmlFor="samples">Amostras</Label>
            <Input
              id="samples"
              type="number"
              min={1}
              max={10}
              value={samples}
              onChange={(e) => setSamples(Number(e.target.value))}
            />
          </div>
        </div>

        <p className="text-sm text-muted-foreground">
          Usa {columnsUsed} de 12 colunas.
          {columnsUsed > 12 ? ' Reduza o número de amostras.' : ''}
          {isNitricOxide
            ? ' Os padrões recebem uma curva µM crescente (0, 10, 20, …), ajustável depois.'
            : ''}
        </p>
      </form>
    </Modal>
  );
}
