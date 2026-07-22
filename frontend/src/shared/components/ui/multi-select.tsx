import { useId } from 'react';
import { Check, ChevronDown } from 'lucide-react';
import { Popover } from '@/shared/components/ui/popover';
import { cn } from '@/shared/lib/utils';

export interface MultiSelectOption {
  value: string;
  label: string;
  /** Optional secondary line (e.g. an e-mail under a name). */
  hint?: string;
}

interface MultiSelectProps {
  options: MultiSelectOption[];
  /** Controlled set of selected option values. */
  selected: string[];
  onChange: (values: string[]) => void;
  /** Trigger placeholder when nothing is selected. */
  placeholder?: string;
  /** Accessible label for the listbox panel. */
  label: string;
  className?: string;
  disabled?: boolean;
}

/**
 * Accessible multi-select (design system). In-house — the app installs no Radix; it composes the existing
 * <Popover> with a checkbox-style `role="listbox"` of `role="option"` rows (`aria-selected`), each a real
 * button so it is keyboard-operable and focusable. The trigger summarises the current selection count. Reusable
 * across the experiments filter bar and the responsible-assignment UI (card [E11]).
 */
export function MultiSelect({
  options,
  selected,
  onChange,
  placeholder = 'Selecionar…',
  label,
  className,
  disabled,
}: MultiSelectProps) {
  const listboxId = useId();
  const selectedSet = new Set(selected);

  const toggle = (value: string) => {
    const next = new Set(selectedSet);
    if (next.has(value)) next.delete(value);
    else next.add(value);
    onChange([...next]);
  };

  const summary =
    selected.length === 0
      ? placeholder
      : selected.length === 1
        ? (options.find((o) => o.value === selected[0])?.label ?? '1 selecionado')
        : `${selected.length} selecionados`;

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
            selected.length === 0 && 'text-muted-foreground',
            className,
          )}
        >
          <span className="truncate">{summary}</span>
          <ChevronDown className="size-4 shrink-0 opacity-50" aria-hidden="true" />
        </button>
      }
    >
      {() => (
        <ul id={listboxId} role="listbox" aria-label={label} aria-multiselectable="true" className="max-h-64 overflow-auto">
          {options.length === 0 ? (
            <li className="px-2 py-3 text-center text-xs text-muted-foreground">Nenhuma opção.</li>
          ) : (
            options.map((option) => {
              const isSelected = selectedSet.has(option.value);
              return (
                <li key={option.value}>
                  <button
                    type="button"
                    role="option"
                    aria-selected={isSelected}
                    onClick={() => toggle(option.value)}
                    className="flex w-full items-start gap-2 rounded-md px-2 py-1.5 text-left text-sm hover:bg-accent"
                  >
                    <span
                      className={cn(
                        'mt-0.5 flex size-4 shrink-0 items-center justify-center rounded border',
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
