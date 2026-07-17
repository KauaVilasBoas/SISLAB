import { useMemo } from 'react';
import { Badge } from '@/shared/components/ui/badge';
import { Field, Select } from '@/modules/inventory/components/form-controls';
import {
  batchExpiryStatus,
  expiryStatusPresentation,
  formatExpiry,
  formatQuantity,
} from '@/modules/inventory/components/stock-presentation';
import type { StockBatchItem } from '@/modules/inventory/types';

interface BatchSelectProps {
  /** FEFO-ordered available batches (as returned by the backend); the first is the FEFO default. */
  batches: StockBatchItem[];
  /** The chosen batch id, or '' for "automatic FEFO" (the empty option). */
  value: string;
  onChange: (batchId: string) => void;
  loading?: boolean;
  id?: string;
}

/**
 * Optional lot picker for the consumption forms (card [E7] #111), shared by the web detail sheet and the
 * mobile quick-consumption flow so both render lots identically. Each option shows the lot code, its
 * month-granularity validity and remaining balance; the empty option keeps the frictionless default:
 * when nothing is chosen the command sends `preferredBatchId = null` and the backend draws FEFO / average
 * cost automatically. An expired lot is surfaced with an amber/red badge under the select but is NOT
 * blocked — the lab rule is "expired only alerts" — reusing the table's colour semantics.
 */
export function BatchSelect({
  batches,
  value,
  onChange,
  loading = false,
  id = 'consume-batch',
}: BatchSelectProps) {
  // The first FEFO row is what the backend would draw automatically — surfaced in the empty option's label
  // so the operator sees which lot the default resolves to without opening the dropdown.
  const fefoDefault = batches[0];

  const selected = useMemo(
    () => batches.find((b) => b.batchId === value),
    [batches, value],
  );

  const selectedStatus = selected
    ? batchExpiryStatus(selected.expiryYear, selected.expiryMonth)
    : null;

  // When no lot is chosen (automatic FEFO), still warn if the lot the backend WOULD draw is expired.
  const effectiveStatus =
    selectedStatus ??
    (fefoDefault ? batchExpiryStatus(fefoDefault.expiryYear, fefoDefault.expiryMonth) : null);

  return (
    <Field label="Lote (opcional)" htmlFor={id}>
      <Select
        id={id}
        value={value}
        onChange={(e) => onChange(e.target.value)}
        disabled={loading || batches.length === 0}
      >
        <option value="">
          {loading
            ? 'Carregando lotes…'
            : batches.length === 0
              ? 'Sem lotes com saldo'
              : fefoDefault
                ? `Automático (FEFO) — ${describeBatch(fefoDefault)}`
                : 'Automático (FEFO)'}
        </option>
        {batches.map((batch) => (
          <option key={batch.batchId} value={batch.batchId}>
            {describeBatch(batch)}
          </option>
        ))}
      </Select>

      {effectiveStatus === 'Expired' || effectiveStatus === 'ExpiringSoon' ? (
        <ExpiryHint status={effectiveStatus} usingDefault={!selected} />
      ) : (
        <p className="text-xs text-muted-foreground">
          Sem seleção, a baixa segue a validade (FEFO) automaticamente.
        </p>
      )}
    </Field>
  );
}

/** One-line description of a lot for the option label: code · validity · remaining balance. */
function describeBatch(batch: StockBatchItem): string {
  const code = batch.lotCode?.trim() || 'sem lote';
  const expiry = formatExpiry(batch.expiryYear, batch.expiryMonth);
  const remaining = formatQuantity(batch.remainingQuantity, batch.unit);
  return `${code} · val. ${expiry} · ${remaining}`;
}

/** Non-blocking validity badge shown under the select (expired only alerts, never blocks). */
function ExpiryHint({
  status,
  usingDefault,
}: {
  status: 'Expired' | 'ExpiringSoon';
  usingDefault: boolean;
}) {
  const presentation = expiryStatusPresentation(status);
  const tone =
    status === 'Expired'
      ? 'border-destructive/40 bg-destructive/10 text-destructive'
      : 'border-amber-500/40 bg-amber-500/10 text-amber-700 dark:text-amber-300';

  const subject = usingDefault ? 'O lote FEFO padrão' : 'Este lote';
  const state = status === 'Expired' ? 'está vencido' : 'vence em breve';

  return (
    <p className={`flex items-center gap-2 rounded-md border px-2.5 py-1.5 text-xs ${tone}`}>
      <Badge variant={presentation.variant} className="shrink-0">
        {presentation.label}
      </Badge>
      <span>{`${subject} ${state} — a baixa é permitida, confira antes de confirmar.`}</span>
    </p>
  );
}
