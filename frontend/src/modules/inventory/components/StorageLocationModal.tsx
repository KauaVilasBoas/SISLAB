import { useMemo, useState, type FormEvent } from 'react';
import { ArrowLeft, Loader2, MapPin, Pencil, Plus, Thermometer } from 'lucide-react';
import { Modal } from '@/shared/components/ui/modal';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import { Badge } from '@/shared/components/ui/badge';
import { cn } from '@/shared/lib/utils';
import type { ApiError } from '@/shared/types/api';
import { useToast } from '@/shared/components/ui/toast';
import { Field, Select } from '@/modules/inventory/components/form-controls';
import {
  storageLocationTypeLabel,
  STORAGE_LOCATION_TYPES,
} from '@/modules/inventory/components/stock-presentation';
import {
  useCreateStorageLocation,
  useManagedStorageLocations,
  useToggleStorageLocationStatus,
  useUpdateStorageLocation,
} from '@/modules/inventory/api/inventory.queries';
import type {
  RegisterStorageLocationRequest,
  StorageLocationListItem,
  StorageLocationType,
  UpdateStorageLocationRequest,
} from '@/modules/inventory/types';

interface StorageLocationModalProps {
  onClose: () => void;
}

/** Which pane the modal shows: the location list or the create/edit form. */
type View =
  | { mode: 'list' }
  | { mode: 'create' }
  | { mode: 'edit'; location: StorageLocationListItem };

/**
 * "Gerenciar locais" management modal (card [E7] #112). A single modal hosts the whole CRUD: the location list
 * (with active/inactive toggle) and an embedded create/edit form, switching panes in place so the operator
 * stays in one dialog. It drives its data from {@link useManagedStorageLocations} (the full listing, distinct
 * from the item-browser summary), and every write invalidates both location caches so the sidebar and the
 * create-item dropdown pick up the change immediately.
 *
 * Business rule: a location's type is fixed at creation (it drives the storage rules and the movement history
 * already recorded against it), so the edit form shows the type read-only. Deactivating preserves the history —
 * an inactive location just can no longer receive stock.
 */
export function StorageLocationModal({ onClose }: StorageLocationModalProps) {
  const [view, setView] = useState<View>({ mode: 'list' });

  const title =
    view.mode === 'create'
      ? 'Novo local de armazenamento'
      : view.mode === 'edit'
        ? 'Editar local de armazenamento'
        : 'Gerenciar locais';

  const description =
    view.mode === 'list'
      ? 'Cadastre, edite e ative/desative os locais onde o laboratório guarda o estoque.'
      : view.mode === 'edit'
        ? 'Atualize o nome e a descrição. O tipo é definido no cadastro e não pode ser alterado.'
        : 'Cadastre um novo local. O tipo é definido agora e não poderá ser alterado depois.';

  return (
    <Modal open onClose={onClose} size="lg" title={title} description={description}>
      {view.mode === 'list' ? (
        <LocationList
          onCreate={() => setView({ mode: 'create' })}
          onEdit={(location) => setView({ mode: 'edit', location })}
        />
      ) : (
        <LocationForm
          location={view.mode === 'edit' ? view.location : undefined}
          onDone={() => setView({ mode: 'list' })}
        />
      )}
    </Modal>
  );
}

/** The location list pane: rows with type/status, an item-count hint and inline edit + active toggle. */
function LocationList({
  onCreate,
  onEdit,
}: {
  onCreate: () => void;
  onEdit: (location: StorageLocationListItem) => void;
}) {
  const query = useManagedStorageLocations();
  const locations = query.data ?? [];

  return (
    <div className="space-y-4">
      <div className="flex justify-end">
        <Button size="sm" onClick={onCreate}>
          <Plus className="size-4" />
          Novo local
        </Button>
      </div>

      {query.isLoading ? (
        <div className="flex items-center justify-center gap-2 py-10 text-sm text-muted-foreground">
          <Loader2 className="size-4 animate-spin" />
          Carregando locais…
        </div>
      ) : query.isError ? (
        <p className="py-10 text-center text-sm text-destructive">
          Não foi possível carregar os locais.
        </p>
      ) : locations.length === 0 ? (
        <p className="py-10 text-center text-sm text-muted-foreground">
          Nenhum local cadastrado. Comece criando o primeiro.
        </p>
      ) : (
        <ul className="divide-y rounded-md border">
          {locations.map((location) => (
            <LocationRow key={location.id} location={location} onEdit={() => onEdit(location)} />
          ))}
        </ul>
      )}
    </div>
  );
}

/** A single location row with its metadata badges and the active/inactive toggle. */
function LocationRow({
  location,
  onEdit,
}: {
  location: StorageLocationListItem;
  onEdit: () => void;
}) {
  const toast = useToast();
  const toggle = useToggleStorageLocationStatus(location.id);

  async function handleToggle() {
    try {
      await toggle.mutateAsync(!location.isActive);
      toast('success', location.isActive ? 'Local desativado.' : 'Local reativado.');
    } catch (err) {
      toast('error', (err as ApiError)?.message ?? 'Não foi possível alterar o status.');
    }
  }

  return (
    <li className="flex items-center gap-3 px-4 py-3">
      <MapPin
        className={cn(
          'size-4 shrink-0',
          location.isActive ? 'text-muted-foreground' : 'text-muted-foreground/40',
        )}
      />

      <div className="min-w-0 flex-1">
        <div className="flex items-center gap-2">
          <span
            className={cn(
              'truncate text-sm font-medium',
              location.isActive ? '' : 'text-muted-foreground line-through',
            )}
          >
            {location.name}
          </span>
          {!location.isActive ? (
            <Badge variant="muted" className="shrink-0">
              Inativo
            </Badge>
          ) : null}
        </div>
        <div className="mt-0.5 flex flex-wrap items-center gap-x-3 gap-y-0.5 text-xs text-muted-foreground">
          <span>{storageLocationTypeLabel(location.type)}</span>
          <span>
            {location.itemCount} {location.itemCount === 1 ? 'item' : 'itens'}
          </span>
          {location.temperatureMinCelsius !== null &&
          location.temperatureMaxCelsius !== null ? (
            <span className="inline-flex items-center gap-1">
              <Thermometer className="size-3" />
              {location.temperatureMinCelsius} °C a {location.temperatureMaxCelsius} °C
            </span>
          ) : null}
        </div>
      </div>

      <Button variant="ghost" size="icon" onClick={onEdit} aria-label={`Editar ${location.name}`}>
        <Pencil className="size-4" />
      </Button>
      <Button
        variant={location.isActive ? 'outline' : 'secondary'}
        size="sm"
        onClick={handleToggle}
        disabled={toggle.isPending}
        className="shrink-0"
      >
        {toggle.isPending ? <Loader2 className="size-4 animate-spin" /> : null}
        {location.isActive ? 'Desativar' : 'Reativar'}
      </Button>
    </li>
  );
}

interface FormState {
  name: string;
  type: StorageLocationType;
  description: string;
  temperatureMin: string;
  temperatureMax: string;
}

function initialFormState(location?: StorageLocationListItem): FormState {
  return {
    name: location?.name ?? '',
    type: location?.type ?? 'GeneralStorage',
    description: location?.description ?? '',
    temperatureMin:
      location?.temperatureMinCelsius !== null && location?.temperatureMinCelsius !== undefined
        ? String(location.temperatureMinCelsius)
        : '',
    temperatureMax:
      location?.temperatureMaxCelsius !== null && location?.temperatureMaxCelsius !== undefined
        ? String(location.temperatureMaxCelsius)
        : '',
  };
}

/** Turns a blank string into null; used for the optional description the backend clears on null/blank. */
function orNull(value: string): string | null {
  const trimmed = value.trim();
  return trimmed === '' ? null : trimmed;
}

/**
 * Create/edit-a-location form pane. On create it posts the full request (name + type + optional
 * description/temperature); on edit it puts only the mutable fields — the type is fixed at creation and shown
 * read-only. The temperature range is only offered for a refrigerated location (the aggregate rejects it for
 * any other type), and both bounds travel together. On success it returns to the list pane.
 */
function LocationForm({
  location,
  onDone,
}: {
  location?: StorageLocationListItem;
  onDone: () => void;
}) {
  const isEdit = Boolean(location);
  const toast = useToast();
  const create = useCreateStorageLocation();
  const update = useUpdateStorageLocation(location?.id ?? '');

  const [form, setForm] = useState<FormState>(() => initialFormState(location));

  const pending = create.isPending || update.isPending;
  const isRefrigerated = form.type === 'Refrigerated';

  function patch<K extends keyof FormState>(key: K, value: FormState[K]) {
    setForm((prev) => ({ ...prev, [key]: value }));
  }

  // Both temperature bounds must be provided together, and the minimum cannot exceed the maximum (mirrors the
  // aggregate's TemperatureRange guard, surfaced client-side so the operator gets an inline message).
  const temperatureError = useMemo(() => {
    if (!isRefrigerated) return null;
    const hasMin = form.temperatureMin !== '';
    const hasMax = form.temperatureMax !== '';
    if (hasMin !== hasMax) return 'Informe a temperatura mínima e máxima juntas.';
    if (hasMin && hasMax && Number(form.temperatureMin) > Number(form.temperatureMax))
      return 'A temperatura mínima não pode ser maior que a máxima.';
    return null;
  }, [isRefrigerated, form.temperatureMin, form.temperatureMax]);

  function readTemperature(): { min: number | null; max: number | null } {
    // A temperature range only exists for a refrigerated location; any other type sends nulls so a type that
    // was refrigerated in a stale form state never leaks a range the backend would reject.
    if (!isRefrigerated || form.temperatureMin === '' || form.temperatureMax === '')
      return { min: null, max: null };
    return { min: Number(form.temperatureMin), max: Number(form.temperatureMax) };
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (temperatureError) {
      toast('error', temperatureError);
      return;
    }

    const temperature = readTemperature();
    try {
      if (isEdit) {
        const body: UpdateStorageLocationRequest = {
          name: form.name.trim(),
          description: orNull(form.description),
          temperatureMinCelsius: temperature.min,
          temperatureMaxCelsius: temperature.max,
        };
        await update.mutateAsync(body);
        toast('success', 'Local atualizado.');
      } else {
        const body: RegisterStorageLocationRequest = {
          name: form.name.trim(),
          type: form.type,
          description: orNull(form.description),
          temperatureMinCelsius: temperature.min,
          temperatureMaxCelsius: temperature.max,
        };
        await create.mutateAsync(body);
        toast('success', 'Local cadastrado.');
      }
      onDone();
    } catch (err) {
      toast('error', (err as ApiError)?.message ?? 'Não foi possível salvar o local.');
    }
  }

  return (
    <form
      className="grid grid-cols-1 gap-4 sm:grid-cols-2"
      onSubmit={handleSubmit}
      noValidate
    >
      <Field label="Nome" htmlFor="location-name" className="sm:col-span-2">
        <Input
          id="location-name"
          value={form.name}
          onChange={(e) => patch('name', e.target.value)}
          placeholder="Ex.: Geladeira 1"
          required
          autoFocus
        />
      </Field>

      <Field label="Tipo" htmlFor="location-type">
        <Select
          id="location-type"
          value={form.type}
          onChange={(e) => patch('type', e.target.value as StorageLocationType)}
          disabled={isEdit}
          required
        >
          {STORAGE_LOCATION_TYPES.map((type) => (
            <option key={type} value={type}>
              {storageLocationTypeLabel(type)}
            </option>
          ))}
        </Select>
        {isEdit ? (
          <p className="text-xs text-muted-foreground">
            O tipo é definido no cadastro e não pode ser alterado.
          </p>
        ) : null}
      </Field>

      <Field label="Descrição (opcional)" htmlFor="location-description" className="sm:col-span-2">
        <Input
          id="location-description"
          value={form.description}
          onChange={(e) => patch('description', e.target.value)}
          placeholder="Ex.: Prateleira central, corredor B"
        />
      </Field>

      {isRefrigerated ? (
        <>
          <Field label="Temperatura mínima (°C)" htmlFor="location-temp-min">
            <Input
              id="location-temp-min"
              type="number"
              step="any"
              inputMode="decimal"
              placeholder="Ex.: -80"
              value={form.temperatureMin}
              onChange={(e) => patch('temperatureMin', e.target.value)}
            />
          </Field>

          <Field label="Temperatura máxima (°C)" htmlFor="location-temp-max">
            <Input
              id="location-temp-max"
              type="number"
              step="any"
              inputMode="decimal"
              placeholder="Ex.: -20"
              value={form.temperatureMax}
              onChange={(e) => patch('temperatureMax', e.target.value)}
            />
          </Field>

          {temperatureError ? (
            <p className="text-xs text-destructive sm:col-span-2">{temperatureError}</p>
          ) : null}
        </>
      ) : null}

      <div className="flex items-center justify-between gap-2 pt-2 sm:col-span-2">
        <Button type="button" variant="ghost" onClick={onDone} disabled={pending}>
          <ArrowLeft className="size-4" />
          Voltar
        </Button>
        <Button type="submit" disabled={pending}>
          {pending ? <Loader2 className="size-4 animate-spin" /> : null}
          {isEdit ? 'Salvar alterações' : 'Cadastrar local'}
        </Button>
      </div>
    </form>
  );
}
