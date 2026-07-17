import { useMemo, useState, type FormEvent, type ReactNode } from 'react';
import { AlertTriangle, Info, Loader2 } from 'lucide-react';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import type { ApiError } from '@/shared/types/api';
import { useToast } from '@/shared/components/ui/toast';
import { Field, Select } from '@/modules/inventory/components/form-controls';
import {
  useDisposeStock,
  useRegisterConsumption,
  useRegisterEntry,
  useStockBatches,
  useStorageLocations,
  useTransferStock,
} from '@/modules/inventory/api/inventory.queries';
import { usePartnerList } from '@/modules/inventory/api/partner.queries';
import { BatchSelect } from '@/modules/inventory/components/BatchSelect';
import { usePermissions } from '@/modules/auth/PermissionsProvider';
import type { StockItemListItem } from '@/modules/inventory/types';

/** Lumen permission code gating the cost fields/columns (card #110). Cost is gestão-sensitive data. */
const COST_READ_PERMISSION = 'Inventory.Cost.Read';

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
  // Only active suppliers can be the origin of a receipt (a Client-only partner never supplies).
  const partners = usePartnerList({}, 1);
  // Cost is gestão-sensitive: only users holding Inventory.Cost.Read may record a unit price (card #110).
  const { hasPermission } = usePermissions();
  const canRecordCost = hasPermission(COST_READ_PERMISSION);
  const [quantity, setQuantity] = useState('');
  const [lotCode, setLotCode] = useState('');
  const [expiryMonth, setExpiryMonth] = useState('');
  const [expiryYear, setExpiryYear] = useState('');
  const [supplierPartnerId, setSupplierPartnerId] = useState('');
  const [unitCost, setUnitCost] = useState('');

  const suppliers = useMemo(
    () =>
      (partners.data?.items ?? []).filter(
        (p) => p.isActive && (p.type === 'Supplier' || p.type === 'Both'),
      ),
    [partners.data],
  );

  // Month and expiry year are month-granularity validity; the backend requires them together (or neither).
  const expiryError = useMemo(() => {
    const hasMonth = expiryMonth !== '';
    const hasYear = expiryYear !== '';
    if (hasMonth !== hasYear) return 'Informe mês e ano de validade juntos.';
    return null;
  }, [expiryMonth, expiryYear]);

  // Unit cost is optional; when informed it must be a non-negative number (mirrors the backend's decimal? >= 0).
  const costError = useMemo(() => {
    if (unitCost === '') return null;
    const parsed = Number(unitCost);
    if (Number.isNaN(parsed) || parsed < 0) return 'Informe um custo válido (maior ou igual a zero).';
    return null;
  }, [unitCost]);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (expiryError) {
      toast('error', expiryError);
      return;
    }
    if (costError) {
      toast('error', costError);
      return;
    }
    try {
      await entry.mutateAsync({
        quantity: Number(quantity),
        unit: item.unit,
        lotCode: lotCode.trim() || null,
        expiryYear: expiryYear === '' ? null : Number(expiryYear),
        expiryMonth: expiryMonth === '' ? null : Number(expiryMonth),
        supplierPartnerId: supplierPartnerId || null,
        occurredOn: null,
        // Only sent when the user may record cost and typed one; otherwise null (donation / no invoice).
        unitCostBrl: canRecordCost && unitCost !== '' ? Number(unitCost) : null,
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

      <div className="grid grid-cols-2 gap-3">
        <Field label="Mês de validade" htmlFor="entry-expiry-month">
          <Input
            id="entry-expiry-month"
            type="number"
            min="1"
            max="12"
            inputMode="numeric"
            placeholder="MM"
            value={expiryMonth}
            onChange={(e) => setExpiryMonth(e.target.value)}
          />
        </Field>
        <Field label="Ano de validade" htmlFor="entry-expiry-year">
          <Input
            id="entry-expiry-year"
            type="number"
            min="2000"
            max="2100"
            inputMode="numeric"
            placeholder="AAAA"
            value={expiryYear}
            onChange={(e) => setExpiryYear(e.target.value)}
          />
        </Field>
      </div>
      {expiryError ? <p className="text-xs text-destructive">{expiryError}</p> : null}

      <Field label="Fornecedor (opcional)" htmlFor="entry-supplier">
        <Select
          id="entry-supplier"
          value={supplierPartnerId}
          onChange={(e) => setSupplierPartnerId(e.target.value)}
        >
          <option value="">
            {partners.isLoading ? 'Carregando…' : 'Sem fornecedor'}
          </option>
          {suppliers.map((p) => (
            <option key={p.id} value={p.id}>
              {p.name}
            </option>
          ))}
        </Select>
      </Field>

      {/* Cost is gestão-only (card #110): the field renders solely for users holding Inventory.Cost.Read. */}
      {canRecordCost ? (
        <Field label="Custo unitário (R$) (opcional)" htmlFor="entry-unit-cost">
          <Input
            id="entry-unit-cost"
            type="number"
            min="0"
            step="0.01"
            inputMode="decimal"
            placeholder="0,00"
            value={unitCost}
            onChange={(e) => setUnitCost(e.target.value)}
          />
          {costError ? <p className="text-xs text-destructive">{costError}</p> : null}
        </Field>
      ) : null}
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
  const batches = useStockBatches(item.id);
  const [quantity, setQuantity] = useState('');
  const [experimentId, setExperimentId] = useState('');
  // '' = automatic FEFO (the backend picks the lot). A specific id draws from that lot first (card #111).
  const [preferredBatchId, setPreferredBatchId] = useState('');

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    try {
      await consume.mutateAsync({
        quantity: Number(quantity),
        unit: item.unit,
        // Sent only when the operator typed an id — the field is optional (no picker until E11).
        experimentId: experimentId.trim() || null,
        occurredOn: null,
        // Null when no lot was chosen: the backend then draws FEFO / average cost automatically (card #111).
        preferredBatchId: preferredBatchId || null,
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

      <BatchSelect
        batches={batches.data ?? []}
        value={preferredBatchId}
        onChange={setPreferredBatchId}
        loading={batches.isLoading}
      />

      <Field label="Experimento (opcional)" htmlFor="consume-experiment">
        <Input
          id="consume-experiment"
          value={experimentId}
          onChange={(e) => setExperimentId(e.target.value)}
          placeholder="ID do experimento"
          autoCapitalize="off"
          autoCorrect="off"
          spellCheck={false}
        />
      </Field>

      <p className="flex items-start gap-2 rounded-md border border-blue-500/30 bg-blue-500/10 px-3 py-2 text-xs text-blue-700 dark:text-blue-300">
        <Info className="mt-0.5 size-3.5 shrink-0" />
        <span>
          Vincule o consumo a um experimento para rastrear o custo por ensaio. A seleção
          de experimentos chega com o módulo Experimentos.
        </span>
      </p>
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
  const [confirmed, setConfirmed] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    // Explicit acknowledgement guard: a disposal is irreversible and audited, so the operator must
    // tick the confirmation before the command is sent.
    if (!confirmed) {
      toast('error', 'Confirme o descarte antes de registrar.');
      return;
    }
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
      pending={dispose.isPending || !confirmed}
      submitLabel="Registrar descarte"
    >
      <p className="flex items-start gap-2 rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 text-xs text-destructive">
        <AlertTriangle className="mt-0.5 size-3.5 shrink-0" />
        <span>
          O descarte baixa o saldo de forma definitiva e fica registrado na auditoria com
          responsável, data e justificativa. Esta ação não pode ser desfeita.
        </span>
      </p>

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

      <label className="flex items-center gap-2 text-sm">
        <input
          type="checkbox"
          checked={confirmed}
          onChange={(e) => setConfirmed(e.target.checked)}
          className="size-4 rounded border-input"
        />
        <span>Confirmo o descarte deste item.</span>
      </label>
    </MovementShell>
  );
}
