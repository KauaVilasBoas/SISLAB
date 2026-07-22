import { useId } from 'react';
import { Check, ChevronDown } from 'lucide-react';
import { Popover } from '@/shared/components/ui/popover';
import { cn } from '@/shared/lib/utils';
import type { MultiSelectOption } from '@/shared/components/ui/multi-select';

interface SingleSelectProps {
  options: MultiSelectOption[];
  /** Controlled selected value, or null when nothing is chosen. */
  value: string | null;
  onChange: (value: string | null) => void;
  /** Trigger placeholder when nothing is selected. */
  placeholder?: string;
  /** Accessible label for the listbox panel. */
  label: string;
  /** When true, choosing the already-selected option clears it (null). Defaults to false. */
  clearable?: boolean;
  className?: string;
  disabled?: boolean;
}

/**
 * Accessible single-select (design system). In-house — the app installs no Radix; it composes the existing
 * <Popover> with a `role="listbox"` of `role="option"` rows (`aria-selected`), each a real button so it is
 * keyboard-operable and focusable. Sibling of <MultiSelect> and shares its {@link MultiSelectOption} shape.
 * Used to pick the experiment's single lead responsible (card [E11]).
 */
export function SingleSelect({
  options,
  value,
  onChange,
  placeholder = 'Selecionar…',
  label,
  clearable = false,
  className,
  disabled,
}: SingleSelectProps) {
  const listboxId = useId();

  const selectedLabel = options.find((o) => o.value === value)?.label ?? placeholder;

  return (
    <Popover
      label={label}
      align="start"
      className="w-64 p-1"
      trigger={
        <button
          type="button"
          disabled={disabled}
          aria-haspopup="listbox"
          aria-controls={listboxId}
          className={cn(
            'flex h-9 w-56 items-center justify-between gap-2 rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-sm',
            'focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring disabled:cursor-not-allowed disabled:opacity-50',
            value == null && 'text-muted-foreground',
            className,
          )}
        >
          <span className="truncate">{selectedLabel}</span>
          <ChevronDown className="size-4 shrink-0 opacity-50" aria-hidden="true" />
        </button>
      }
    >
      {(close) => (
        <ul id={listboxId} role="listbox" aria-label={label} className="max-h-64 overflow-auto">
          {options.length === 0 ? (
            <li className="px-2 py-3 text-center text-xs text-muted-foreground">Nenhuma opção.</li>
          ) : (
            options.map((option) => {
              const isSelected = option.value === value;
              return (
                <li key={option.value}>
                  <button
                    type="button"
                    role="option"
                    aria-selected={isSelected}
                    onClick={() => {
                      onChange(isSelected && clearable ? null : option.value);
                      close();
                    }}
                    className="flex w-full items-start gap-2 rounded-md px-2 py-1.5 text-left text-sm hover:bg-accent"
                  >
                    <span
                      className={cn(
                        'mt-0.5 flex size-4 shrink-0 items-center justify-center rounded-full border',
                        isSelected ? 'border-primary bg-primary text-primary-foreground' : 'border-input',
                      )}
                      aria-hidden="true"
                    >
                      {isSelected && <Check className="size-3" />}
                    </span>
                    <span className="min-w-0">
                      <span className="block truncate">{option.label}</span>
                      {option.hint && (
                        <span className="block truncate text-xs text-muted-foreground">{option.hint}</span>
                      )}
                    </span>
                  </button>
                </li>
              );
            })
          )}
        </ul>
      )}
    </Popover>
  );
}
