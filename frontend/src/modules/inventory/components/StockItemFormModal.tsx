import { useMemo, useState, type FormEvent } from 'react';
import { Loader2 } from 'lucide-react';
import { Modal } from '@/shared/components/ui/modal';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import type { ApiError } from '@/shared/types/api';
import { useToast } from '@/shared/components/ui/toast';
import { Field, Select } from '@/modules/inventory/components/form-controls';
import {
  useItemCategories,
  useRegisterStockItem,
  useStorageLocations,
  useUnits,
} from '@/modules/inventory/api/inventory.queries';
import type { RegisterStockItemRequest } from '@/modules/inventory/types';

interface StockItemFormModalProps {
  onClose: () => void;
}

interface FormState {
  name: string;
  categoryId: string;
  storageLocationId: string;
  unit: string;
  initialQuantity: string;
  minimumQuantity: string;
  brand: string;
  application: string;
  lotCode: string;
  expiryMonth: string;
  expiryYear: string;
  isControlled: boolean;
}

const EMPTY: FormState = {
  name: '',
  categoryId: '',
  storageLocationId: '',
  unit: '',
  initialQuantity: '',
  minimumQuantity: '',
  brand: '',
  application: '',
  lotCode: '',
  expiryMonth: '',
  expiryYear: '',
  isControlled: false,
};

/** Turns a blank string into null; used for the optional text/number fields the backend accepts. */
function orNull(value: string): string | null {
  const trimmed = value.trim();
  return trimmed === '' ? null : trimmed;
}

/**
 * Create-a-stock-item form (card [E7] #46). Drives its category/location/unit dropdowns from the
 * per-tenant Configuration catalogues and posts a RegisterStockItemRequest. On success the mutation
 * invalidates the item list and the modal closes.
 *
 * There is deliberately no "edit item" variant: the Inventory backend exposes no update endpoint —
 * an existing item is mutated only through its stock movements (entry, consumption, transfer,
 * disposal), surfaced from the detail panel.
 */
export function StockItemFormModal({ onClose }: StockItemFormModalProps) {
  const toast = useToast();
  const register = useRegisterStockItem();
  const categories = useItemCategories();
  const locations = useStorageLocations();
  const units = useUnits();

  const [form, setForm] = useState<FormState>(EMPTY);

  const referenceLoading = categories.isLoading || locations.isLoading || units.isLoading;

  function patch<K extends keyof FormState>(key: K, value: FormState[K]) {
    setForm((prev) => ({ ...prev, [key]: value }));
  }

  const expiryError = useMemo(() => {
    const hasMonth = form.expiryMonth !== '';
    const hasYear = form.expiryYear !== '';
    if (hasMonth !== hasYear) return 'Informe mês e ano de validade juntos.';
    return null;
  }, [form.expiryMonth, form.expiryYear]);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (expiryError) {
      toast('error', expiryError);
      return;
    }

    const body: RegisterStockItemRequest = {
      name: form.name.trim(),
      categoryId: form.categoryId,
      storageLocationId: form.storageLocationId,
      unit: form.unit,
      initialQuantity: Number(form.initialQuantity),
      minimumQuantity: Number(form.minimumQuantity),
      isControlled: form.isControlled,
      brand: orNull(form.brand),
      application: orNull(form.application),
      lotCode: orNull(form.lotCode),
      expiryYear: form.expiryYear === '' ? null : Number(form.expiryYear),
      expiryMonth: form.expiryMonth === '' ? null : Number(form.expiryMonth),
    };

    try {
      await register.mutateAsync(body);
      toast('success', 'Item de estoque cadastrado.');
      onClose();
    } catch (err) {
      toast('error', (err as ApiError)?.message ?? 'Não foi possível cadastrar o item.');
    }
  }

  return (
    <Modal
      open
      onClose={onClose}
      size="lg"
      title="Novo item de estoque"
      description="Cadastre um item com seu saldo inicial, unidade e validade."
      footer={
        <>
          <Button variant="outline" onClick={onClose} disabled={register.isPending}>
            Cancelar
          </Button>
          <Button
            type="submit"
            form="stock-item-form"
            disabled={register.isPending || referenceLoading}
          >
            {register.isPending && <Loader2 className="size-4 animate-spin" />}
            Cadastrar item
          </Button>
        </>
      }
    >
      <form
        id="stock-item-form"
        className="grid grid-cols-1 gap-4 sm:grid-cols-2"
        onSubmit={handleSubmit}
        noValidate
      >
        <Field label="Nome" htmlFor="item-name" className="sm:col-span-2">
          <Input
            id="item-name"
            value={form.name}
            onChange={(e) => patch('name', e.target.value)}
            placeholder="Ex.: Álcool etílico 70%"
            required
            autoFocus
          />
        </Field>

        <Field label="Categoria" htmlFor="item-category">
          <Select
            id="item-category"
            value={form.categoryId}
            onChange={(e) => patch('categoryId', e.target.value)}
            required
          >
            <option value="" disabled>
              {categories.isLoading ? 'Carregando…' : 'Selecione a categoria'}
            </option>
            {(categories.data ?? []).map((c) => (
              <option key={c.id} value={c.id}>
                {c.name}
              </option>
            ))}
          </Select>
        </Field>

        <Field label="Local de armazenamento" htmlFor="item-location">
          <Select
            id="item-location"
            value={form.storageLocationId}
            onChange={(e) => patch('storageLocationId', e.target.value)}
            required
          >
            <option value="" disabled>
              {locations.isLoading ? 'Carregando…' : 'Selecione o local'}
            </option>
            {(locations.data ?? []).map((l) => (
              <option key={l.id} value={l.id}>
                {l.name}
              </option>
            ))}
          </Select>
        </Field>

        <Field label="Unidade" htmlFor="item-unit">
          <Select
            id="item-unit"
            value={form.unit}
            onChange={(e) => patch('unit', e.target.value)}
            required
          >
            <option value="" disabled>
              {units.isLoading ? 'Carregando…' : 'Selecione a unidade'}
            </option>
            {(units.data ?? []).map((u) => (
              <option key={u.id} value={u.symbol}>
                {u.symbol} — {u.name}
              </option>
            ))}
          </Select>
        </Field>

        <Field label="Quantidade inicial" htmlFor="item-initial">
          <Input
            id="item-initial"
            type="number"
            min="0"
            step="any"
            inputMode="decimal"
            value={form.initialQuantity}
            onChange={(e) => patch('initialQuantity', e.target.value)}
            required
          />
        </Field>

        <Field label="Quantidade mínima" htmlFor="item-minimum">
          <Input
            id="item-minimum"
            type="number"
            min="0"
            step="any"
            inputMode="decimal"
            value={form.minimumQuantity}
            onChange={(e) => patch('minimumQuantity', e.target.value)}
            required
          />
        </Field>

        <Field label="Marca (opcional)" htmlFor="item-brand">
          <Input
            id="item-brand"
            value={form.brand}
            onChange={(e) => patch('brand', e.target.value)}
          />
        </Field>

        <Field label="Lote (opcional)" htmlFor="item-lot">
          <Input
            id="item-lot"
            value={form.lotCode}
            onChange={(e) => patch('lotCode', e.target.value)}
          />
        </Field>

        <Field
          label="Aplicação (opcional)"
          htmlFor="item-application"
          className="sm:col-span-2"
        >
          <Input
            id="item-application"
            value={form.application}
            onChange={(e) => patch('application', e.target.value)}
            placeholder="Ex.: Uso em bancada"
          />
        </Field>

        <Field label="Mês de validade (opcional)" htmlFor="item-expiry-month">
          <Input
            id="item-expiry-month"
            type="number"
            min="1"
            max="12"
            inputMode="numeric"
            placeholder="MM"
            value={form.expiryMonth}
            onChange={(e) => patch('expiryMonth', e.target.value)}
          />
        </Field>

        <Field label="Ano de validade (opcional)" htmlFor="item-expiry-year">
          <Input
            id="item-expiry-year"
            type="number"
            min="2000"
            max="2100"
            inputMode="numeric"
            placeholder="AAAA"
            value={form.expiryYear}
            onChange={(e) => patch('expiryYear', e.target.value)}
          />
        </Field>

        {expiryError ? (
          <p className="text-xs text-destructive sm:col-span-2">{expiryError}</p>
        ) : null}

        <label className="flex items-center gap-2 sm:col-span-2">
          <input
            type="checkbox"
            checked={form.isControlled}
            onChange={(e) => patch('isControlled', e.target.checked)}
            className="size-4 rounded border-input"
          />
          <span className="text-sm">Item controlado (rastreabilidade reforçada)</span>
        </label>
      </form>
    </Modal>
  );
}
