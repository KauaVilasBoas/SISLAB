import { useState, type FormEvent } from 'react';
import { Filter, Loader2, Plus } from 'lucide-react';
import { Button } from '@/shared/components/ui/button';
import { Card, CardContent } from '@/shared/components/ui/card';
import { Modal } from '@/shared/components/ui/modal';
import { Input } from '@/shared/components/ui/input';
import { Label } from '@/shared/components/ui/label';
import { useToast } from '@/shared/components/ui/toast';
import type { ApiError } from '@/shared/types/api';
import { RequirePermission } from '@/modules/auth/PermissionsProvider';
import { Permissions } from '@/modules/auth/permissions';
import type { ComparisonOperator } from '@/modules/configuration/types';
import {
  useCreateInclusionCriterion,
  useInclusionCriteria,
} from '@/modules/configuration/api/configuration.queries';
import {
  CatalogueEmpty,
  CatalogueError,
  CatalogueLoading,
} from '@/modules/configuration/components/CatalogueState';

/**
 * The comparison operators offered in the create form and used to render a criterion as a human-readable rule
 * (SISLAB-02). The symbol is the operator's mathematical glyph; the value mirrors the backend enum name exactly.
 */
const OPERATOR_OPTIONS: { value: ComparisonOperator; symbol: string; label: string }[] = [
  { value: 'GreaterThanOrEqual', symbol: '≥', label: 'Maior ou igual (≥)' },
  { value: 'GreaterThan', symbol: '>', label: 'Maior (>)' },
  { value: 'LessThanOrEqual', symbol: '≤', label: 'Menor ou igual (≤)' },
  { value: 'LessThan', symbol: '<', label: 'Menor (<)' },
  { value: 'Equal', symbol: '=', label: 'Igual (=)' },
];

/** Resolves an operator's mathematical symbol from the raw backend enum name, falling back to the name. */
function operatorSymbol(operator: ComparisonOperator): string {
  return OPERATOR_OPTIONS.find((option) => option.value === operator)?.symbol ?? operator;
}

/**
 * "Critérios de inclusão" tab (SISLAB-02): lists the active company's configurable selection rules
 * `(parâmetro, operador, limiar, unidade)` — e.g. glicemia ≥ 250 mg/dL — and cadasters new ones. The rules feed
 * the batch "Aplicar seleção" flow on the in vivo project screen; the threshold/parameter are never hardcoded.
 */
export function InclusionCriteriaTab() {
  const criteria = useInclusionCriteria();
  const [createOpen, setCreateOpen] = useState(false);

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-end">
        <RequirePermission code={Permissions.configuration.createInclusionCriterion}>
          <Button onClick={() => setCreateOpen(true)}>
            <Plus className="size-4" />
            Novo critério
          </Button>
        </RequirePermission>
      </div>

      {criteria.isLoading ? (
        <CatalogueLoading label="Carregando critérios de inclusão…" />
      ) : criteria.isError ? (
        <CatalogueError label="Não foi possível carregar os critérios de inclusão." />
      ) : criteria.data && criteria.data.length > 0 ? (
        <Card>
          <CardContent className="p-0">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b text-left text-xs uppercase tracking-wide text-muted-foreground">
                  <th className="px-5 py-3 font-medium">Parâmetro</th>
                  <th className="px-5 py-3 font-medium">Regra de inclusão</th>
                </tr>
              </thead>
              <tbody>
                {criteria.data.map((criterion) => (
                  <tr key={criterion.id} className="border-b last:border-0">
                    <td className="px-5 py-3 font-medium">{criterion.parameterCode}</td>
                    <td className="px-5 py-3 text-muted-foreground">
                      {operatorSymbol(criterion.operator)} {criterion.threshold}{' '}
                      {criterion.unit}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </CardContent>
        </Card>
      ) : (
        <CatalogueEmpty
          icon={<Filter className="size-8" />}
          message="Nenhum critério cadastrado. Crie a primeira regra de inclusão (ex.: glicemia ≥ 250 mg/dL)."
        />
      )}

      {createOpen ? (
        <CreateInclusionCriterionModal onClose={() => setCreateOpen(false)} />
      ) : null}
    </div>
  );
}

function CreateInclusionCriterionModal({ onClose }: { onClose: () => void }) {
  const create = useCreateInclusionCriterion();
  const toast = useToast();
  const [parameterCode, setParameterCode] = useState('');
  const [operator, setOperator] = useState<ComparisonOperator>('GreaterThanOrEqual');
  const [threshold, setThreshold] = useState('');
  const [unit, setUnit] = useState('');

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    const parsedThreshold = Number(threshold.trim());
    if (!Number.isFinite(parsedThreshold)) {
      toast('error', 'Informe um limiar numérico válido.');
      return;
    }

    try {
      await create.mutateAsync({
        parameterCode: parameterCode.trim(),
        operator,
        threshold: parsedThreshold,
        unit: unit.trim(),
      });
      toast('success', 'Critério de inclusão criado com sucesso.');
      onClose();
    } catch (err) {
      toast('error', (err as ApiError)?.message ?? 'Não foi possível criar o critério.');
    }
  }

  return (
    <Modal
      open
      onClose={onClose}
      title="Novo critério de inclusão"
      description="Regra que seleciona os animais a partir das leituras: parâmetro, operador, limiar e unidade."
      footer={
        <>
          <Button variant="outline" onClick={onClose} disabled={create.isPending}>
            Cancelar
          </Button>
          <Button type="submit" form="create-criterion-form" disabled={create.isPending}>
            {create.isPending && <Loader2 className="size-4 animate-spin" />}
            Criar critério
          </Button>
        </>
      }
    >
      <form
        id="create-criterion-form"
        className="flex flex-col gap-4"
        onSubmit={handleSubmit}
        noValidate
      >
        <div className="flex flex-col gap-2">
          <Label htmlFor="criterion-parameter">Parâmetro</Label>
          <Input
            id="criterion-parameter"
            placeholder="glicemia"
            value={parameterCode}
            onChange={(e) => setParameterCode(e.target.value)}
            required
            autoFocus
          />
          <p className="text-xs text-muted-foreground">
            Código do parâmetro medido (ex.: glicemia). Só se aplica às levas cujo modelo
            o inclui.
          </p>
        </div>
        <div className="grid grid-cols-2 gap-4">
          <div className="flex flex-col gap-2">
            <Label htmlFor="criterion-operator">Operador</Label>
            <select
              id="criterion-operator"
              className="h-9 rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-sm focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
              value={operator}
              onChange={(e) => setOperator(e.target.value as ComparisonOperator)}
            >
              {OPERATOR_OPTIONS.map((option) => (
                <option key={option.value} value={option.value}>
                  {option.label}
                </option>
              ))}
            </select>
          </div>
          <div className="flex flex-col gap-2">
            <Label htmlFor="criterion-threshold">Limiar</Label>
            <Input
              id="criterion-threshold"
              type="number"
              step="any"
              inputMode="decimal"
              placeholder="250"
              value={threshold}
              onChange={(e) => setThreshold(e.target.value)}
              required
            />
          </div>
        </div>
        <div className="flex flex-col gap-2">
          <Label htmlFor="criterion-unit">Unidade</Label>
          <Input
            id="criterion-unit"
            placeholder="mg/dL"
            value={unit}
            onChange={(e) => setUnit(e.target.value)}
            required
          />
        </div>
      </form>
    </Modal>
  );
}
