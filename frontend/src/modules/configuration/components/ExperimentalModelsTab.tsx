import { useState, type FormEvent } from 'react';
import { FlaskConical, Loader2, Plus, Trash2 } from 'lucide-react';
import { Button } from '@/shared/components/ui/button';
import { Card, CardContent } from '@/shared/components/ui/card';
import { Modal } from '@/shared/components/ui/modal';
import { Input } from '@/shared/components/ui/input';
import { Label } from '@/shared/components/ui/label';
import { useToast } from '@/shared/components/ui/toast';
import type { ApiError } from '@/shared/types/api';
import { RequirePermission } from '@/modules/auth/PermissionsProvider';
import { Permissions } from '@/modules/auth/permissions';
import type {
  ExperimentalModelListItem,
  StandardGroup,
  StandardGroupKind,
} from '@/modules/configuration/types';
import {
  useCreateExperimentalModel,
  useExperimentalModels,
} from '@/modules/configuration/api/configuration.queries';
import {
  CatalogueEmpty,
  CatalogueError,
  CatalogueLoading,
} from '@/modules/configuration/components/CatalogueState';

/**
 * The recurring physiological/behavioural parameters offered as checkboxes (SISLAB-04). The codes are the
 * canonical, lab-agnostic values the backend stores and SISLAB-02 will later consult per model to decide which
 * readings to offer ("glicemia só no diabético"). Adding a new parameter is a one-line edit here.
 */
const PARAMETER_OPTIONS: { code: string; label: string }[] = [
  { code: 'glicemia', label: 'Glicemia' },
  { code: 'rotarod', label: 'Rotarod' },
  { code: 'peso', label: 'Peso' },
];

/** "Modelos experimentais" tab: lists per-tenant induction protocols and cadasters new ones. */
export function ExperimentalModelsTab() {
  const models = useExperimentalModels();
  const [createOpen, setCreateOpen] = useState(false);

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-end">
        <RequirePermission code={Permissions.configuration.createExperimentalModel}>
          <Button onClick={() => setCreateOpen(true)}>
            <Plus className="size-4" />
            Novo modelo
          </Button>
        </RequirePermission>
      </div>

      {models.isLoading ? (
        <CatalogueLoading label="Carregando modelos experimentais…" />
      ) : models.isError ? (
        <CatalogueError label="Não foi possível carregar os modelos experimentais." />
      ) : models.data && models.data.length > 0 ? (
        <Card>
          <CardContent className="p-0">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b text-left text-xs uppercase tracking-wide text-muted-foreground">
                  <th className="px-5 py-3 font-medium">Modelo</th>
                  <th className="px-5 py-3 font-medium">Indução</th>
                  <th className="px-5 py-3 font-medium">Dia de referência</th>
                </tr>
              </thead>
              <tbody>
                {models.data.map((model: ExperimentalModelListItem) => (
                  <tr key={model.id} className="border-b last:border-0">
                    <td className="px-5 py-3">
                      <div className="font-medium">{model.name}</div>
                      {model.description ? (
                        <div className="text-xs text-muted-foreground">{model.description}</div>
                      ) : null}
                    </td>
                    <td className="px-5 py-3 text-muted-foreground">
                      {model.inductionAdministrations}{' '}
                      {model.inductionAdministrations === 1 ? 'administração' : 'administrações'}
                    </td>
                    <td className="px-5 py-3 text-muted-foreground">
                      D+{model.referenceDayAfterInduction}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </CardContent>
        </Card>
      ) : (
        <CatalogueEmpty
          icon={<FlaskConical className="size-8" />}
          message="Nenhum modelo cadastrado. Crie o primeiro modelo experimental (protocolo de indução)."
        />
      )}

      {createOpen ? <CreateExperimentalModelModal onClose={() => setCreateOpen(false)} /> : null}
    </div>
  );
}

/** Editable draft of one standard group in the create form. */
interface GroupDraft {
  name: string;
  kind: StandardGroupKind;
  doseAmount: string;
  doseUnit: string;
}

const DEFAULT_GROUPS: GroupDraft[] = [
  { name: 'Naive', kind: 'Naive', doseAmount: '', doseUnit: '' },
  { name: 'Controle', kind: 'Control', doseAmount: '', doseUnit: '' },
];

function CreateExperimentalModelModal({ onClose }: { onClose: () => void }) {
  const create = useCreateExperimentalModel();
  const toast = useToast();

  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [administrations, setAdministrations] = useState('1');
  const [intervalDays, setIntervalDays] = useState('1');
  const [referenceDay, setReferenceDay] = useState('28');
  const [timepoints, setTimepoints] = useState('');
  const [parameters, setParameters] = useState<Set<string>>(new Set());
  const [groups, setGroups] = useState<GroupDraft[]>(DEFAULT_GROUPS);
  const [microlitresPerGram, setMicrolitresPerGram] = useState('10');
  const [defaultDiluent, setDefaultDiluent] = useState('');

  function toggleParameter(code: string) {
    setParameters((current) => {
      const next = new Set(current);
      if (next.has(code)) next.delete(code);
      else next.add(code);
      return next;
    });
  }

  function updateGroup(index: number, patch: Partial<GroupDraft>) {
    setGroups((current) =>
      current.map((group, i) => (i === index ? { ...group, ...patch } : group)),
    );
  }

  function addGroup() {
    setGroups((current) => [
      ...current,
      { name: '', kind: 'Dose', doseAmount: '', doseUnit: '' },
    ]);
  }

  function removeGroup(index: number) {
    setGroups((current) => current.filter((_, i) => i !== index));
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    const parsedTimepoints = timepoints
      .split(',')
      .map((label) => label.trim())
      .filter((label) => label.length > 0);

    if (parsedTimepoints.length === 0) {
      toast('error', 'Informe ao menos um timepoint (separe por vírgula).');
      return;
    }

    const parsedGroups: StandardGroup[] = groups
      .map((group) => ({
        name: group.name.trim(),
        kind: group.kind,
        doseAmount:
          group.kind === 'Dose' ? parseNumber(group.doseAmount) : null,
        doseUnit: group.kind === 'Dose' ? group.doseUnit.trim() || null : null,
      }))
      .filter((group) => group.name.length > 0);

    if (parsedGroups.length === 0) {
      toast('error', 'Defina ao menos um grupo-padrão.');
      return;
    }

    const invalidDose = parsedGroups.find(
      (group) => group.kind === 'Dose' && (group.doseAmount === null || !group.doseUnit),
    );
    if (invalidDose) {
      toast('error', `Informe dose e unidade para o grupo "${invalidDose.name}".`);
      return;
    }

    try {
      await create.mutateAsync({
        name: name.trim(),
        description: description.trim() || null,
        induction: {
          administrations: Number(administrations) || 1,
          intervalDays: Number(intervalDays) || 0,
          referenceDayAfterInduction: Number(referenceDay) || 0,
        },
        timepoints: parsedTimepoints,
        parameters: [...parameters],
        groups: parsedGroups,
        dilutionDefaults: {
          microlitresPerGram: parseNumber(microlitresPerGram) ?? 0,
          defaultDiluent: defaultDiluent.trim(),
        },
      });
      toast('success', 'Modelo experimental criado com sucesso.');
      onClose();
    } catch (err) {
      toast('error', (err as ApiError)?.message ?? 'Não foi possível criar o modelo.');
    }
  }

  return (
    <Modal
      open
      onClose={onClose}
      size="lg"
      title="Novo modelo experimental"
      description="Protocolo de indução, timepoints, parâmetros aplicáveis, grupos-padrão e diluição padrão do laboratório."
      footer={
        <>
          <Button variant="outline" onClick={onClose} disabled={create.isPending}>
            Cancelar
          </Button>
          <Button type="submit" form="create-model-form" disabled={create.isPending}>
            {create.isPending && <Loader2 className="size-4 animate-spin" />}
            Criar modelo
          </Button>
        </>
      }
    >
      <form id="create-model-form" className="flex flex-col gap-5" onSubmit={handleSubmit} noValidate>
        <div className="flex flex-col gap-2">
          <Label htmlFor="model-name">Nome</Label>
          <Input
            id="model-name"
            placeholder="Neuropatia diabética"
            value={name}
            onChange={(e) => setName(e.target.value)}
            required
            autoFocus
          />
        </div>
        <div className="flex flex-col gap-2">
          <Label htmlFor="model-description">Descrição (opcional)</Label>
          <Input
            id="model-description"
            placeholder="Modelo ND: 1ª/2ª indução e leitura no 28° dia."
            value={description}
            onChange={(e) => setDescription(e.target.value)}
          />
        </div>

        <fieldset className="space-y-3 rounded-lg border p-4">
          <legend className="px-1 text-sm font-medium">Protocolo de indução</legend>
          <div className="grid grid-cols-3 gap-4">
            <div className="flex flex-col gap-2">
              <Label htmlFor="model-administrations">Administrações</Label>
              <Input
                id="model-administrations"
                type="number"
                min="1"
                inputMode="numeric"
                value={administrations}
                onChange={(e) => setAdministrations(e.target.value)}
              />
            </div>
            <div className="flex flex-col gap-2">
              <Label htmlFor="model-interval">Intervalo (dias)</Label>
              <Input
                id="model-interval"
                type="number"
                min="0"
                inputMode="numeric"
                value={intervalDays}
                onChange={(e) => setIntervalDays(e.target.value)}
              />
            </div>
            <div className="flex flex-col gap-2">
              <Label htmlFor="model-reference-day">Dia de referência</Label>
              <Input
                id="model-reference-day"
                type="number"
                min="0"
                inputMode="numeric"
                value={referenceDay}
                onChange={(e) => setReferenceDay(e.target.value)}
              />
            </div>
          </div>
        </fieldset>

        <div className="flex flex-col gap-2">
          <Label htmlFor="model-timepoints">Timepoints padrão</Label>
          <Input
            id="model-timepoints"
            placeholder="Separe por vírgula: basal, pós-indução, 7 dias, 15 dias, 21 dias, 28 dias"
            value={timepoints}
            onChange={(e) => setTimepoints(e.target.value)}
            required
          />
        </div>

        <div className="flex flex-col gap-2">
          <Label>Parâmetros aplicáveis</Label>
          <div className="flex flex-wrap gap-4">
            {PARAMETER_OPTIONS.map((option) => (
              <label key={option.code} className="flex items-center gap-2 text-sm">
                <input
                  type="checkbox"
                  className="size-4 rounded border-input"
                  checked={parameters.has(option.code)}
                  onChange={() => toggleParameter(option.code)}
                />
                {option.label}
              </label>
            ))}
          </div>
          <p className="text-xs text-muted-foreground">
            Os parâmetros marcados serão oferecidos nas leituras da leva; os demais ficam ocultos.
          </p>
        </div>

        <fieldset className="space-y-3 rounded-lg border p-4">
          <legend className="px-1 text-sm font-medium">Grupos-padrão</legend>
          <div className="space-y-3">
            {groups.map((group, index) => (
              <div key={index} className="flex flex-wrap items-end gap-3">
                <div className="flex min-w-32 flex-1 flex-col gap-1.5">
                  <Label htmlFor={`group-name-${index}`} className="text-xs">
                    Nome
                  </Label>
                  <Input
                    id={`group-name-${index}`}
                    placeholder="3 g/kg"
                    value={group.name}
                    onChange={(e) => updateGroup(index, { name: e.target.value })}
                  />
                </div>
                <div className="flex w-32 flex-col gap-1.5">
                  <Label htmlFor={`group-kind-${index}`} className="text-xs">
                    Tipo
                  </Label>
                  <select
                    id={`group-kind-${index}`}
                    className="h-9 rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-sm focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
                    value={group.kind}
                    onChange={(e) =>
                      updateGroup(index, { kind: e.target.value as StandardGroupKind })
                    }
                  >
                    <option value="Naive">Naive</option>
                    <option value="Control">Controle</option>
                    <option value="Dose">Dose</option>
                  </select>
                </div>
                {group.kind === 'Dose' && (
                  <>
                    <div className="flex w-24 flex-col gap-1.5">
                      <Label htmlFor={`group-dose-${index}`} className="text-xs">
                        Dose
                      </Label>
                      <Input
                        id={`group-dose-${index}`}
                        type="number"
                        step="any"
                        inputMode="decimal"
                        placeholder="3"
                        value={group.doseAmount}
                        onChange={(e) => updateGroup(index, { doseAmount: e.target.value })}
                      />
                    </div>
                    <div className="flex w-24 flex-col gap-1.5">
                      <Label htmlFor={`group-unit-${index}`} className="text-xs">
                        Unidade
                      </Label>
                      <Input
                        id={`group-unit-${index}`}
                        placeholder="g/kg"
                        value={group.doseUnit}
                        onChange={(e) => updateGroup(index, { doseUnit: e.target.value })}
                      />
                    </div>
                  </>
                )}
                <Button
                  type="button"
                  variant="ghost"
                  size="icon"
                  aria-label={`Remover grupo ${group.name || index + 1}`}
                  onClick={() => removeGroup(index)}
                  disabled={groups.length === 1}
                >
                  <Trash2 className="size-4" />
                </Button>
              </div>
            ))}
          </div>
          <Button type="button" variant="outline" size="sm" onClick={addGroup}>
            <Plus className="size-4" />
            Adicionar grupo
          </Button>
        </fieldset>

        <fieldset className="space-y-3 rounded-lg border p-4">
          <legend className="px-1 text-sm font-medium">Diluição padrão</legend>
          <div className="grid grid-cols-2 gap-4">
            <div className="flex flex-col gap-2">
              <Label htmlFor="model-ratio">Relação (µL por g)</Label>
              <Input
                id="model-ratio"
                type="number"
                step="any"
                min="0"
                inputMode="decimal"
                placeholder="5"
                value={microlitresPerGram}
                onChange={(e) => setMicrolitresPerGram(e.target.value)}
              />
            </div>
            <div className="flex flex-col gap-2">
              <Label htmlFor="model-diluent">Diluente padrão</Label>
              <Input
                id="model-diluent"
                placeholder="Óleo de soja"
                value={defaultDiluent}
                onChange={(e) => setDefaultDiluent(e.target.value)}
                required
              />
            </div>
          </div>
          <p className="text-xs text-muted-foreground">
            Ex.: 1 g de animal : 5 µL de solução. Usado como padrão no preparo in vivo (SISLAB-01).
          </p>
        </fieldset>
      </form>
    </Modal>
  );
}

/** Parses a numeric input into a number, mapping blank/invalid text to null. */
function parseNumber(value: string): number | null {
  const trimmed = value.trim();
  if (trimmed.length === 0) return null;
  const parsed = Number(trimmed);
  return Number.isFinite(parsed) ? parsed : null;
}
