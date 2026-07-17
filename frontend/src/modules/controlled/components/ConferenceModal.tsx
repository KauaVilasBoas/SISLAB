import { useMemo, useState, type FormEvent } from 'react';
import { Loader2 } from 'lucide-react';
import { Modal } from '@/shared/components/ui/modal';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import { useToast } from '@/shared/components/ui/toast';
import type { ApiError } from '@/shared/types/api';
import { Field } from '@/modules/inventory/components/form-controls';
import { formatQuantity } from '@/modules/inventory/components/stock-presentation';
import { useRegisterConference } from '@/modules/controlled/api/controlled.queries';
import { formatDivergence } from '@/modules/controlled/components/controlled-presentation';
import type { ControlledItem } from '@/modules/controlled/types';

interface ConferenceModalProps {
  /** The controlled item being counted; its unit fixes the counted quantity's unit. */
  item: ControlledItem;
  onClose: () => void;
}

/**
 * Conference (physical count) modal for a controlled item (card [E7] #62). Shows the system balance and
 * takes the physically counted quantity; it previews the divergence BEFORE confirming so the operator
 * sees a mismatch up front. On confirm it dispatches RegisterStockCount, which records the count and the
 * divergence in the append-only trail WITHOUT changing the balance. The responsável is the authenticated
 * user (never sent); the counted unit is the item's canonical unit, which the backend requires.
 */
export function ConferenceModal({ item, onClose }: ConferenceModalProps) {
  const toast = useToast();
  const conference = useRegisterConference(item.id);
  const [counted, setCounted] = useState('');

  // Preview divergence = counted − system balance; null until a valid number is typed.
  const previewDivergence = useMemo(() => {
    if (counted.trim() === '') return null;
    const value = Number(counted);
    if (!Number.isFinite(value)) return null;
    return value - item.quantity;
  }, [counted, item.quantity]);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    try {
      const divergence = await conference.mutateAsync({
        countedQuantity: Number(counted),
        unit: item.unit,
        occurredOn: null,
      });
      toast(
        'success',
        divergence === 0
          ? 'Conferência registrada — saldo conferido, sem divergência.'
          : `Conferência registrada — divergência de ${formatDivergence(divergence, item.unit)}.`,
      );
      onClose();
    } catch (err) {
      toast(
        'error',
        (err as ApiError)?.message ?? 'Não foi possível registrar a conferência.',
      );
    }
  }

  const mismatched = previewDivergence !== null && previewDivergence !== 0;

  return (
    <Modal
      open
      onClose={onClose}
      title="Registrar conferência"
      description={`Contagem física de ${item.name}. O saldo não é alterado — apenas a divergência é registrada na trilha.`}
      footer={
        <>
          <Button variant="ghost" onClick={onClose} disabled={conference.isPending}>
            Cancelar
          </Button>
          <Button type="submit" form="conference-form" disabled={conference.isPending}>
            {conference.isPending ? <Loader2 className="size-4 animate-spin" /> : null}
            Confirmar conferência
          </Button>
        </>
      }
    >
      <form id="conference-form" onSubmit={handleSubmit} noValidate className="space-y-4">
        <div className="flex items-center justify-between rounded-lg border bg-muted/30 px-3 py-2">
          <span className="text-sm text-muted-foreground">Saldo do sistema</span>
          <span className="text-sm font-semibold">
            {formatQuantity(item.quantity, item.unit)}
          </span>
        </div>

        <Field label={`Saldo físico contado (${item.unit})`} htmlFor="conference-counted">
          <Input
            id="conference-counted"
            type="number"
            min="0"
            step="any"
            inputMode="decimal"
            value={counted}
            onChange={(e) => setCounted(e.target.value)}
            required
            autoFocus
          />
        </Field>

        {previewDivergence !== null ? (
          <div
            className={
              mismatched
                ? 'rounded-lg border border-destructive/30 bg-destructive/10 px-3 py-2'
                : 'rounded-lg border border-emerald-200 bg-emerald-50 px-3 py-2 dark:border-emerald-800 dark:bg-emerald-950'
            }
          >
            <div className="flex items-center justify-between">
              <span className="text-sm text-muted-foreground">Divergência</span>
              <span className={mismatched ? 'text-sm font-semibold text-destructive' : 'text-sm font-semibold'}>
                {formatDivergence(previewDivergence, item.unit)}
              </span>
            </div>
            <p className="mt-1 text-xs text-muted-foreground">
              {mismatched
                ? 'Há divergência entre a contagem física e o saldo do sistema. O registro será mantido para compliance.'
                : 'Contagem física confere com o saldo do sistema.'}
            </p>
          </div>
        ) : null}
      </form>
    </Modal>
  );
}
