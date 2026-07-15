import { useState, type FormEvent, type ReactNode } from 'react';
import { Loader2 } from 'lucide-react';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import type { ApiError } from '@/shared/types/api';
import { useToast } from '@/shared/components/ui/toast';
import { Field, Select } from '@/modules/inventory/components/form-controls';
import {
  useDisposeStock,
  useRegisterConsumption,
  useRegisterEntry,
  useStorageLocations,
  useTransferStock,
} from '@/modules/inventory/api/inventory.queries';
import type { StockItemListItem } from '@/modules/inventory/types';

type MovementKind = 'entry' | 'consumption' | 'transfer' | 'disposal';

interface StockMovementFormsProps {
  kind: MovementKind;
  item: StockItemListItem;
  onDone: () => void;
}

/**
 * Inline movement forms for the detail sheet. One presentational shell (title + fields + submit)
 * switches on the movement kind; each branch owns its mutation and toasts success/failure. Quantity
 * movements default the unit to the item's canonical unit, which the backend requires on entry,
 * consumption and disposal.
 */
export function StockMovementForms({ kind, item, onDone }: StockMovementFormsProps) {
  switch (kind) {
    case 'entry':
      return <EntryForm item={item} onDone={onDone} />;
    case 'consumption':
      return <ConsumptionForm item={item} onDone={onDone} />;
    case 'transfer':
      return <TransferForm item={item} onDone={onDone} />;
    case 'disposal':
      return <DisposalForm item={item} onDone={onDone} />;
    default:
      return null;
  }
}

function MovementShell({
  title,
  onSubmit,
  pending,
  submitLabel,
  children,
}: {
  title: string;
  onSubmit: (event: FormEvent<HTMLFormElement>) => void;
  pending: boolean;
  submitLabel: string;
  children: ReactNode;
}) {
  return (
    <form
      onSubmit={onSubmit}
      noValidate
      className="space-y-3 rounded-lg border bg-muted/30 p-3"
    >
      <p className="text-sm font-medium">{title}</p>
      {children}
      <Button type="submit" size="sm" className="w-full" disabled={pending}>
        {pending && <Loader2 className="size-4 animate-spin" />}
        {submitLabel}
      </Button>
    </form>
  );
}

function EntryForm({ item, onDone }: { item: StockItemListItem; onDone: () => void }) {
  const toast = useToast();
  const entry = useRegisterEntry(item.id);
  const [quantity, setQuantity] = useState('');
  const [lotCode, setLotCode] = useState('');

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    try {
      await entry.mutateAsync({
        quantity: Number(quantity),
        unit: item.unit,
        lotCode: lotCode.trim() || null,
        expiryYear: null,
        expiryMonth: null,
        supplierPartnerId: null,
        occurredOn: null,
      });
      toast('success', 'Entrada registrada.');
      onDone();
    } catch (err) {
      toast(
        'error',
        (err as ApiError)?.message ?? 'Não foi possível registrar a entrada.',
      );
    }
  }

  return (
    <MovementShell
      title="Registrar entrada"
      onSubmit={handleSubmit}
      pending={entry.isPending}
      submitLabel="Registrar entrada"
    >
      <Field label={`Quantidade (${item.unit})`} htmlFor="entry-qty">
        <Input
          id="entry-qty"
          type="number"
          min="0"
          step="any"
          inputMode="decimal"
          value={quantity}
          onChange={(e) => setQuantity(e.target.value)}
          required
          autoFocus
        />
      </Field>
      <Field label="Lote (opcional)" htmlFor="entry-lot">
        <Input
          id="entry-lot"
          value={lotCode}
          onChange={(e) => setLotCode(e.target.value)}
        />
      </Field>
    </MovementShell>
  );
}

function ConsumptionForm({
  item,
  onDone,
}: {
  item: StockItemListItem;
  onDone: () => void;
}) {
  const toast = useToast();
  const consume = useRegisterConsumption(item.id);
  const [quantity, setQuantity] = useState('');

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    try {
      await consume.mutateAsync({
        quantity: Number(quantity),
        unit: item.unit,
        experimentId: null,
        occurredOn: null,
      });
      toast('success', 'Consumo registrado.');
      onDone();
    } catch (err) {
      toast(
        'error',
        (err as ApiError)?.message ?? 'Não foi possível registrar o consumo.',
      );
    }
  }

  return (
    <MovementShell
      title="Registrar consumo"
      onSubmit={handleSubmit}
      pending={consume.isPending}
      submitLabel="Registrar consumo"
    >
      <Field label={`Quantidade (${item.unit})`} htmlFor="consume-qty">
        <Input
          id="consume-qty"
          type="number"
          min="0"
          step="any"
          inputMode="decimal"
          value={quantity}
          onChange={(e) => setQuantity(e.target.value)}
          required
          autoFocus
        />
      </Field>
    </MovementShell>
  );
}

function TransferForm({ item, onDone }: { item: StockItemListItem; onDone: () => void }) {
  const toast = useToast();
  const transfer = useTransferStock(item.id);
  const locations = useStorageLocations();
  const [toLocationId, setToLocationId] = useState('');

  const targets = (locations.data ?? []).filter((l) => l.id !== item.storageLocationId);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    try {
      await transfer.mutateAsync({
        fromLocationId: item.storageLocationId,
        toLocationId,
        occurredOn: null,
      });
      toast('success', 'Item transferido.');
      onDone();
    } catch (err) {
      toast('error', (err as ApiError)?.message ?? 'Não foi possível transferir o item.');
    }
  }

  return (
    <MovementShell
      title="Transferir para outro local"
      onSubmit={handleSubmit}
      pending={transfer.isPending}
      submitLabel="Transferir"
    >
      <Field label="Local de destino" htmlFor="transfer-to">
        <Select
          id="transfer-to"
          value={toLocationId}
          onChange={(e) => setToLocationId(e.target.value)}
          required
        >
          <option value="" disabled>
            {locations.isLoading ? 'Carregando…' : 'Selecione o destino'}
          </option>
          {targets.map((l) => (
            <option key={l.id} value={l.id}>
              {l.name}
            </option>
          ))}
        </Select>
      </Field>
    </MovementShell>
  );
}

function DisposalForm({ item, onDone }: { item: StockItemListItem; onDone: () => void }) {
  const toast = useToast();
  const dispose = useDisposeStock(item.id);
  const [quantity, setQuantity] = useState('');
  const [reason, setReason] = useState('');

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    try {
      await dispose.mutateAsync({
        quantity: Number(quantity),
        unit: item.unit,
        reason: reason.trim(),
        occurredOn: null,
      });
      toast('success', 'Descarte registrado.');
      onDone();
    } catch (err) {
      toast(
        'error',
        (err as ApiError)?.message ?? 'Não foi possível registrar o descarte.',
      );
    }
  }

  return (
    <MovementShell
      title="Registrar descarte"
      onSubmit={handleSubmit}
      pending={dispose.isPending}
      submitLabel="Registrar descarte"
    >
      <Field label={`Quantidade (${item.unit})`} htmlFor="dispose-qty">
        <Input
          id="dispose-qty"
          type="number"
          min="0"
          step="any"
          inputMode="decimal"
          value={quantity}
          onChange={(e) => setQuantity(e.target.value)}
          required
          autoFocus
        />
      </Field>
      <Field label="Justificativa" htmlFor="dispose-reason">
        <Input
          id="dispose-reason"
          value={reason}
          onChange={(e) => setReason(e.target.value)}
          placeholder="Ex.: Vencido / contaminado"
          required
        />
      </Field>
    </MovementShell>
  );
}
