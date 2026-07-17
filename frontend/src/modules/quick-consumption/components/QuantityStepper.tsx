import { Minus, Plus } from 'lucide-react';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';

interface QuantityStepperProps {
  /** Current quantity to consume, in the item's canonical unit. */
  value: number;
  /** Canonical unit symbol shown alongside the value (e.g. mg, mL, un). */
  unit: string;
  /** On-hand balance — the stepper never lets the quantity exceed it. */
  max: number;
  /** Increment/decrement step (defaults to 1). */
  step?: number;
  onChange: (value: number) => void;
}

/**
 * Dumb −/value/+ stepper for the consumption quantity, in the item's real unit (card [E7] #63).
 *
 * Clamps to (0, max]: it never goes below the step (a consumption of zero makes no sense) nor above the
 * on-hand balance — the obvious client-side guard. The authoritative rule (e.g. exact balance, unit
 * conversion) stays on the backend; this only prevents the operator from submitting a plainly invalid
 * amount. The middle field stays editable so large amounts can be typed instead of tapped.
 */
export function QuantityStepper({ value, unit, max, step = 1, onChange }: QuantityStepperProps) {
  const clamp = (next: number) => Math.min(Math.max(next, 0), max);

  function handleType(raw: string) {
    const parsed = Number(raw);
    onChange(Number.isFinite(parsed) ? clamp(parsed) : 0);
  }

  return (
    <div className="flex items-stretch gap-2">
      <Button
        type="button"
        variant="outline"
        size="icon"
        className="size-11 shrink-0"
        aria-label="Diminuir quantidade"
        disabled={value <= step}
        onClick={() => onChange(clamp(value - step))}
      >
        <Minus />
      </Button>

      <div className="relative flex-1">
        <Input
          type="number"
          inputMode="decimal"
          min="0"
          max={max}
          step="any"
          value={Number.isFinite(value) ? value : ''}
          onChange={(e) => handleType(e.target.value)}
          aria-label={`Quantidade a consumir em ${unit}`}
          className="h-11 pr-12 text-center text-lg font-semibold"
        />
        <span className="pointer-events-none absolute inset-y-0 right-3 flex items-center text-sm text-muted-foreground">
          {unit}
        </span>
      </div>

      <Button
        type="button"
        variant="outline"
        size="icon"
        className="size-11 shrink-0"
        aria-label="Aumentar quantidade"
        disabled={value >= max}
        onClick={() => onChange(clamp(value + step))}
      >
        <Plus />
      </Button>
    </div>
  );
}
