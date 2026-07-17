import { useState, type FormEvent } from 'react';
import { Activity, Loader2, Plus } from 'lucide-react';
import { Button } from '@/shared/components/ui/button';
import { Card, CardContent } from '@/shared/components/ui/card';
import { Modal } from '@/shared/components/ui/modal';
import { Input } from '@/shared/components/ui/input';
import { Label } from '@/shared/components/ui/label';
import { useToast } from '@/shared/components/ui/toast';
import type { ApiError } from '@/shared/types/api';
import type { ReferenceRangeListItem } from '@/modules/configuration/types';
import {
  useCreateReferenceRange,
  useReferenceRanges,
} from '@/modules/configuration/api/configuration.queries';
import {
  CatalogueEmpty,
  CatalogueError,
  CatalogueLoading,
} from '@/modules/configuration/components/CatalogueState';

/** Renders a reference range's numeric bounds as a human-readable interval. */
function formatBounds(range: ReferenceRangeListItem): string {
  const { minimum, maximum, unit } = range;
  const suffix = unit ? ` ${unit}` : '';
  if (minimum !== null && maximum !== null) return `${minimum} – ${maximum}${suffix}`;
  if (minimum !== null) return `≥ ${minimum}${suffix}`;
  if (maximum !== null) return `≤ ${maximum}${suffix}`;
  return '—';
}

/** "Reference ranges" tab: lists analyte intervals and creates new ones. */
export function ReferenceRangesTab() {
  const ranges = useReferenceRanges();
  const [createOpen, setCreateOpen] = useState(false);

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-end">
        <Button onClick={() => setCreateOpen(true)}>
          <Plus className="size-4" />
          Nova faixa
        </Button>
      </div>

      {ranges.isLoading ? (
        <CatalogueLoading label="Carregando faixas de referência…" />
      ) : ranges.isError ? (
        <CatalogueError label="Não foi possível carregar as faixas de referência." />
      ) : ranges.data && ranges.data.length > 0 ? (
        <Card>
          <CardContent className="p-0">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b text-left text-xs uppercase tracking-wide text-muted-foreground">
                  <th className="px-5 py-3 font-medium">Parâmetro</th>
                  <th className="px-5 py-3 font-medium">Espécie</th>
                  <th className="px-5 py-3 font-medium">Faixa</th>
                </tr>
              </thead>
              <tbody>
                {ranges.data.map((range) => (
                  <tr key={range.id} className="border-b last:border-0">
                    <td className="px-5 py-3 font-medium">{range.analyte}</td>
                    <td className="px-5 py-3 text-muted-foreground">{range.species}</td>
                    <td className="px-5 py-3 text-muted-foreground">{formatBounds(range)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </CardContent>
        </Card>
      ) : (
        <CatalogueEmpty
          icon={<Activity className="size-8" />}
          message="Nenhuma faixa cadastrada. Crie a primeira faixa de referência."
        />
      )}

      {createOpen ? <CreateReferenceRangeModal onClose={() => setCreateOpen(false)} /> : null}
    </div>
  );
}

/** Parses a numeric input into a number, mapping blank/invalid text to null. */
function parseOptionalNumber(value: string): number | null {
  const trimmed = value.trim();
  if (trimmed.length === 0) return null;
  const parsed = Number(trimmed);
  return Number.isFinite(parsed) ? parsed : null;
}

function CreateReferenceRangeModal({ onClose }: { onClose: () => void }) {
  const create = useCreateReferenceRange();
  const toast = useToast();
  const [analyte, setAnalyte] = useState('');
  const [species, setSpecies] = useState('');
  const [minimum, setMinimum] = useState('');
  const [maximum, setMaximum] = useState('');
  const [unit, setUnit] = useState('');

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    try {
      await create.mutateAsync({
        analyte: analyte.trim(),
        species: species.trim(),
        minimum: parseOptionalNumber(minimum),
        maximum: parseOptionalNumber(maximum),
        unit: unit.trim() || null,
      });
      toast('success', 'Faixa de referência criada com sucesso.');
      onClose();
    } catch (err) {
      toast('error', (err as ApiError)?.message ?? 'Não foi possível criar a faixa de referência.');
    }
  }

  return (
    <Modal
      open
      onClose={onClose}
      title="Nova faixa de referência"
      description="Defina o intervalo saudável de um parâmetro, por espécie."
      footer={
        <>
          <Button variant="outline" onClick={onClose} disabled={create.isPending}>
            Cancelar
          </Button>
          <Button type="submit" form="create-range-form" disabled={create.isPending}>
            {create.isPending && <Loader2 className="size-4 animate-spin" />}
            Criar faixa
          </Button>
        </>
      }
    >
      <form id="create-range-form" className="flex flex-col gap-4" onSubmit={handleSubmit} noValidate>
        <div className="flex flex-col gap-2">
          <Label htmlFor="range-analyte">Parâmetro</Label>
          <Input
            id="range-analyte"
            placeholder="Glicose"
            value={analyte}
            onChange={(e) => setAnalyte(e.target.value)}
            required
            autoFocus
          />
        </div>
        <div className="flex flex-col gap-2">
          <Label htmlFor="range-species">Espécie</Label>
          <Input
            id="range-species"
            placeholder="Camundongo"
            value={species}
            onChange={(e) => setSpecies(e.target.value)}
            required
          />
        </div>
        <div className="grid grid-cols-2 gap-4">
          <div className="flex flex-col gap-2">
            <Label htmlFor="range-min">Mínimo</Label>
            <Input
              id="range-min"
              type="number"
              step="any"
              inputMode="decimal"
              placeholder="70"
              value={minimum}
              onChange={(e) => setMinimum(e.target.value)}
            />
          </div>
          <div className="flex flex-col gap-2">
            <Label htmlFor="range-max">Máximo</Label>
            <Input
              id="range-max"
              type="number"
              step="any"
              inputMode="decimal"
              placeholder="110"
              value={maximum}
              onChange={(e) => setMaximum(e.target.value)}
            />
          </div>
        </div>
        <div className="flex flex-col gap-2">
          <Label htmlFor="range-unit">Unidade (opcional)</Label>
          <Input
            id="range-unit"
            placeholder="mg/dL"
            value={unit}
            onChange={(e) => setUnit(e.target.value)}
          />
        </div>
      </form>
    </Modal>
  );
}
