import { useState, type FormEvent } from 'react';
import { Loader2, User } from 'lucide-react';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import { Field } from '@/modules/inventory/components/form-controls';
import { useRegisterConsumption } from '@/modules/inventory/api/inventory.queries';
import { useAuth } from '@/modules/auth/AuthProvider';
import { useToast } from '@/shared/components/ui/toast';
import type { ApiError } from '@/shared/types/api';
import { QuantityStepper } from '@/modules/quick-consumption/components/QuantityStepper';
import type { StockItemDetail } from '@/modules/quick-consumption/types';

interface ConsumptionFormProps {
  item: StockItemDetail;
  /** Called after a successful consumption so the parent can reset the flow for the next scan. */
  onConfirmed: () => void;
}

/** ISO yyyy-MM-dd for today, in local time — the default business date of the consumption. */
function todayIso(): string {
  const now = new Date();
  const month = String(now.getMonth() + 1).padStart(2, '0');
  const day = String(now.getDate()).padStart(2, '0');
  return `${now.getFullYear()}-${month}-${day}`;
}

/**
 * The consumption form of the quick flow (card [E7] #63): a quantity stepper in the item's real unit,
 * an optional experiment reference, the business date (default today) and the read-only responsible
 * (the signed-in user — never selectable, decision #24/#47). Submitting fires `RegisterConsumption`,
 * whose handler writes the controlled-item audit log (#57) and emits the event that feeds the
 * per-experiment report (#31) — this form just sends the command.
 *
 * On the experiment field: the Experiments module (E11) is not built yet, so there is no list to pick
 * from. Rather than block this card, the field is a minimal optional id input, sent only when filled —
 * the picker arrives with E11. TODO(#E11): replace with an experiment selector.
 */
export function ConsumptionForm({ item, onConfirmed }: ConsumptionFormProps) {
  const { user } = useAuth();
  const toast = useToast();
  const consume = useRegisterConsumption(item.id);

  const [quantity, setQuantity] = useState(item.quantity > 0 ? 1 : 0);
  const [experimentId, setExperimentId] = useState('');
  const [occurredOn, setOccurredOn] = useState(todayIso());

  const today = todayIso();
  const responsible = user?.username || user?.email || 'Você';
  const outOfStock = item.quantity <= 0;
  const canSubmit = quantity > 0 && quantity <= item.quantity && occurredOn <= today;

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!canSubmit) return;

    try {
      await consume.mutateAsync({
        quantity,
        unit: item.unit,
        // Sent only when the operator typed an id — the field is optional (no picker until E11).
        experimentId: experimentId.trim() || null,
        occurredOn,
      });
      toast('success', `Baixa de ${quantity} ${item.unit} registrada.`);
      onConfirmed();
    } catch (err) {
      toast('error', (err as ApiError)?.message ?? 'Não foi possível registrar a baixa.');
    }
  }

  return (
    <form onSubmit={handleSubmit} noValidate className="space-y-4">
      <Field label={`Quantidade a consumir (${item.unit})`} htmlFor="qc-quantity">
        <QuantityStepper
          value={quantity}
          unit={item.unit}
          max={item.quantity}
          onChange={setQuantity}
        />
        {outOfStock ? (
          <p className="text-xs font-medium text-destructive">
            Sem saldo disponível para baixa.
          </p>
        ) : (
          <p className="text-xs text-muted-foreground">
            Saldo disponível: {item.quantity} {item.unit}
          </p>
        )}
      </Field>

      <Field label="Experimento (opcional)" htmlFor="qc-experiment">
        <Input
          id="qc-experiment"
          value={experimentId}
          onChange={(e) => setExperimentId(e.target.value)}
          placeholder="ID do experimento (opcional)"
          autoCapitalize="off"
          autoCorrect="off"
          spellCheck={false}
        />
        <p className="text-xs text-muted-foreground">
          A seleção de experimentos chega com o módulo Experimentos.
        </p>
      </Field>

      <Field label="Data" htmlFor="qc-date">
        <Input
          id="qc-date"
          type="date"
          max={today}
          value={occurredOn}
          onChange={(e) => setOccurredOn(e.target.value)}
          required
        />
      </Field>

      <div className="flex items-center gap-2 rounded-lg border bg-muted/30 px-3 py-2 text-sm">
        <User className="size-4 shrink-0 text-muted-foreground" />
        <span className="min-w-0">
          <span className="text-muted-foreground">Responsável: </span>
          <span className="font-medium">{responsible}</span>
        </span>
      </div>

      <Button
        type="submit"
        size="lg"
        className="h-12 w-full text-base"
        disabled={!canSubmit || consume.isPending}
      >
        {consume.isPending && <Loader2 className="size-4 animate-spin" />}
        Confirmar baixa
      </Button>
    </form>
  );
}
