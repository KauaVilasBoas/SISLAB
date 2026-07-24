import { useMemo, useState, type FormEvent } from 'react';
import { Loader2 } from 'lucide-react';
import { Modal } from '@/shared/components/ui/modal';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import { Label } from '@/shared/components/ui/label';
import { Switch } from '@/shared/components/ui/switch';
import { useToast } from '@/shared/components/ui/toast';
import type { ApiError } from '@/shared/types/api';
import type { CompoundState, GroupDetail } from '@/modules/in-vivo/types';
import { compoundStateLabel } from '@/modules/in-vivo/presentation';
import { usePrepareGroupSolution } from '@/modules/in-vivo/api/projects.queries';

const STATES: CompoundState[] = ['Powder', 'Liquid'];

/**
 * Solution-preparation form (SISLAB-01): confirms a dose group's in vivo preparation — the frozen dose × weight ×
 * g:µL-relation snapshot the operator pipettes. Two shapes share the form:
 *  - Controle (só veículo): only the relation (µL per g of animal) and the animal-weight basis; no compound.
 *  - Braço dosado: dose (g/kg) + group weight + compound state, plus density when the compound is Liquid.
 * All laboratory-specific values (relation, density, state) are inputs — nothing is hardcoded. The group weight is
 * pre-filled from the sum of the group's animal weights when available, but stays editable.
 */
export function PrepareSolutionModal({
  projectId,
  batchId,
  group,
  onClose,
}: {
  projectId: string;
  batchId: string;
  group: GroupDetail;
  onClose: () => void;
}) {
  const toast = useToast();
  const prepare = usePrepareGroupSolution(projectId, batchId, group.id);

  // Sum of the group's known animal weights — a sensible default for the weight basis (the operator can override).
  const animalWeightSum = useMemo(
    () => group.animals.reduce((total, animal) => total + (animal.weightGrams ?? 0), 0),
    [group.animals],
  );
  const defaultWeight = animalWeightSum > 0 ? String(Number(animalWeightSum.toFixed(2))) : '';

  const [isVehicleOnly, setIsVehicleOnly] = useState(group.doseAmount === 0);
  const [relation, setRelation] = useState('5');
  const [relationWeight, setRelationWeight] = useState(defaultWeight);
  const [dose, setDose] = useState(group.doseAmount > 0 ? String(group.doseAmount) : '');
  const [groupWeight, setGroupWeight] = useState(defaultWeight);
  const [state, setState] = useState<CompoundState>('Liquid');
  const [density, setDensity] = useState('');

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    try {
      await prepare.mutateAsync({
        isVehicleOnly,
        relationMicrolitresPerGram: Number(relation),
        relationWeightGrams: Number(relationWeight),
        doseAmountGramsPerKilogram: isVehicleOnly ? undefined : Number(dose),
        groupWeightGrams: isVehicleOnly ? undefined : Number(groupWeight),
        state: isVehicleOnly ? undefined : state,
        densityGramsPerMillilitre:
          isVehicleOnly || state !== 'Liquid' || density.trim() === ''
            ? undefined
            : Number(density),
      });
      toast('success', 'Preparo confirmado.');
      onClose();
    } catch (err) {
      toast('error', (err as ApiError)?.message ?? 'Não foi possível confirmar o preparo.');
    }
  }

  return (
    <Modal
      open
      onClose={onClose}
      size="lg"
      title={`Preparar solução — ${group.name}`}
      description="Dose × peso do grupo e relação g:µL. O snapshot é congelado e rastreável ao ser confirmado."
      footer={
        <>
          <Button variant="outline" onClick={onClose} disabled={prepare.isPending}>
            Cancelar
          </Button>
          <Button type="submit" form="prepare-solution-form" disabled={prepare.isPending}>
            {prepare.isPending && <Loader2 className="size-4 animate-spin" />}
            Confirmar preparo
          </Button>
        </>
      }
    >
      <form
        id="prepare-solution-form"
        className="space-y-5"
        onSubmit={handleSubmit}
        noValidate
      >
        <label className="flex items-start justify-between gap-4 rounded-lg border bg-muted/30 p-3">
          <span>
            <span className="block text-sm font-medium">Controle (só veículo)</span>
            <span className="block text-xs text-muted-foreground">
              Sem composto — toda a solução é diluente (nenhuma subtração de volume).
            </span>
          </span>
          <Switch
            checked={isVehicleOnly}
            onCheckedChange={setIsVehicleOnly}
            aria-label="Controle (só veículo)"
          />
        </label>

        {/* Relation (µL per g of animal) + weight basis — required for both shapes. */}
        <div className="grid grid-cols-2 gap-3">
          <div className="space-y-1.5">
            <Label htmlFor="prep-relation">Relação (µL por g de animal)</Label>
            <Input
              id="prep-relation"
              type="number"
              min={0}
              step="any"
              value={relation}
              onChange={(e) => setRelation(e.target.value)}
              placeholder="Ex.: 5"
              required
            />
            <p className="text-xs text-muted-foreground">1 g : {relation || '—'} µL</p>
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="prep-relation-weight">Peso-base do grupo (g)</Label>
            <Input
              id="prep-relation-weight"
              type="number"
              min={0}
              step="any"
              value={relationWeight}
              onChange={(e) => setRelationWeight(e.target.value)}
              placeholder="Ex.: 156"
              required
            />
            {animalWeightSum > 0 && (
              <p className="text-xs text-muted-foreground">
                Soma dos animais: {Number(animalWeightSum.toFixed(2))} g
              </p>
            )}
          </div>
        </div>

        {/* Treatment-arm fields — hidden for the Controle (vehicle-only) shape. */}
        {!isVehicleOnly && (
          <div className="space-y-5 rounded-lg border p-4">
            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-1.5">
                <Label htmlFor="prep-dose">Dose (g/kg)</Label>
                <Input
                  id="prep-dose"
                  type="number"
                  min={0}
                  step="any"
                  value={dose}
                  onChange={(e) => setDose(e.target.value)}
                  placeholder="Ex.: 3"
                  required
                />
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="prep-group-weight">Peso do grupo p/ dose (g)</Label>
                <Input
                  id="prep-group-weight"
                  type="number"
                  min={0}
                  step="any"
                  value={groupWeight}
                  onChange={(e) => setGroupWeight(e.target.value)}
                  placeholder="Ex.: 189.6"
                  required
                />
              </div>
            </div>

            <div className="space-y-1.5">
              <Label>Estado do composto</Label>
              <div className="grid grid-cols-2 gap-2">
                {STATES.map((option) => {
                  const selected = option === state;
                  return (
                    <button
                      key={option}
                      type="button"
                      onClick={() => setState(option)}
                      aria-pressed={selected}
                      className={
                        selected
                          ? 'rounded-md border border-primary bg-primary/5 px-3 py-2 text-sm font-medium text-primary'
                          : 'rounded-md border px-3 py-2 text-sm text-muted-foreground hover:border-primary/50'
                      }
                    >
                      {compoundStateLabel[option]}
                    </button>
                  );
                })}
              </div>
              <p className="text-xs text-muted-foreground">
                {state === 'Liquid'
                  ? 'Líquido: o volume do composto (via densidade) é subtraído do volume final.'
                  : 'Pó: não ocupa volume — nada é subtraído do volume final.'}
              </p>
            </div>

            {state === 'Liquid' && (
              <div className="space-y-1.5">
                <Label htmlFor="prep-density">Densidade do composto (g/mL)</Label>
                <Input
                  id="prep-density"
                  type="number"
                  min={0}
                  step="any"
                  value={density}
                  onChange={(e) => setDensity(e.target.value)}
                  placeholder="Ex.: 0.9865"
                  required
                />
              </div>
            )}
          </div>
        )}
      </form>
    </Modal>
  );
}
