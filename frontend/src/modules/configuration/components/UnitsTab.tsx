import { useState, type FormEvent } from 'react';
import { Loader2, Plus, Ruler } from 'lucide-react';
import { Button } from '@/shared/components/ui/button';
import { Card, CardContent } from '@/shared/components/ui/card';
import { Modal } from '@/shared/components/ui/modal';
import { Input } from '@/shared/components/ui/input';
import { Label } from '@/shared/components/ui/label';
import { useToast } from '@/shared/components/ui/toast';
import type { ApiError } from '@/shared/types/api';
import { useCreateUnit, useUnits } from '@/modules/configuration/api/configuration.queries';
import {
  CatalogueEmpty,
  CatalogueError,
  CatalogueLoading,
} from '@/modules/configuration/components/CatalogueState';

/** "Units of measure" tab: lists units and creates new ones (symbol + name). */
export function UnitsTab() {
  const units = useUnits();
  const [createOpen, setCreateOpen] = useState(false);

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-end">
        <Button onClick={() => setCreateOpen(true)}>
          <Plus className="size-4" />
          Nova unidade
        </Button>
      </div>

      {units.isLoading ? (
        <CatalogueLoading label="Carregando unidades…" />
      ) : units.isError ? (
        <CatalogueError label="Não foi possível carregar as unidades." />
      ) : units.data && units.data.length > 0 ? (
        <Card>
          <CardContent className="p-0">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b text-left text-xs uppercase tracking-wide text-muted-foreground">
                  <th className="px-5 py-3 font-medium">Símbolo</th>
                  <th className="px-5 py-3 font-medium">Nome</th>
                </tr>
              </thead>
              <tbody>
                {units.data.map((unit) => (
                  <tr key={unit.id} className="border-b last:border-0">
                    <td className="px-5 py-3 font-medium">{unit.symbol}</td>
                    <td className="px-5 py-3 text-muted-foreground">{unit.name}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </CardContent>
        </Card>
      ) : (
        <CatalogueEmpty
          icon={<Ruler className="size-8" />}
          message="Nenhuma unidade cadastrada. Crie a primeira unidade de medida."
        />
      )}

      {createOpen ? <CreateUnitModal onClose={() => setCreateOpen(false)} /> : null}
    </div>
  );
}

function CreateUnitModal({ onClose }: { onClose: () => void }) {
  const create = useCreateUnit();
  const toast = useToast();
  const [symbol, setSymbol] = useState('');
  const [name, setName] = useState('');

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    try {
      await create.mutateAsync({ symbol: symbol.trim(), name: name.trim() });
      toast('success', 'Unidade criada com sucesso.');
      onClose();
    } catch (err) {
      toast('error', (err as ApiError)?.message ?? 'Não foi possível criar a unidade.');
    }
  }

  return (
    <Modal
      open
      onClose={onClose}
      title="Nova unidade"
      description="Defina a unidade de medida ou consumo usada nos itens do estoque."
      footer={
        <>
          <Button variant="outline" onClick={onClose} disabled={create.isPending}>
            Cancelar
          </Button>
          <Button type="submit" form="create-unit-form" disabled={create.isPending}>
            {create.isPending && <Loader2 className="size-4 animate-spin" />}
            Criar unidade
          </Button>
        </>
      }
    >
      <form id="create-unit-form" className="flex flex-col gap-4" onSubmit={handleSubmit} noValidate>
        <div className="flex flex-col gap-2">
          <Label htmlFor="unit-symbol">Símbolo</Label>
          <Input
            id="unit-symbol"
            placeholder="mL"
            value={symbol}
            onChange={(e) => setSymbol(e.target.value)}
            required
            autoFocus
          />
        </div>
        <div className="flex flex-col gap-2">
          <Label htmlFor="unit-name">Nome</Label>
          <Input
            id="unit-name"
            placeholder="Mililitro"
            value={name}
            onChange={(e) => setName(e.target.value)}
            required
          />
        </div>
      </form>
    </Modal>
  );
}
