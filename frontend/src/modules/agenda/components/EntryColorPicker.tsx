import { Check } from 'lucide-react';
import { Popover } from '@/shared/components/ui/popover';
import { cn } from '@/shared/lib/utils';
import { ENTRY_COLOR_PALETTE } from '@/modules/agenda/presentation';

/**
 * Google-Calendar-style colour picker for a calendar entry (card [E10.12]). A circular trigger fills with the
 * entry's current colour (or a neutral "default" swatch when none is set); clicking opens a grid of predefined
 * swatches plus a "Default" row that clears the override back to the automatic activity-type colour.
 *
 * The picker is presentational: it owns no colour state, delegating every change to {@link EntryColorPickerProps.onChange}
 * (`null` = use the automatic colour). Selecting closes the popover, mirroring Google Calendar.
 */
export interface EntryColorPickerProps {
  /** The current '#rrggbb' colour, or null when the entry uses the automatic activity-type colour. */
  value: string | null;
  /** Called with the chosen colour, or null to clear the override. */
  onChange: (color: string | null) => void;
}

export function EntryColorPicker({ value, onChange }: EntryColorPickerProps) {
  return (
    <Popover
      align="start"
      label="Escolher cor do evento"
      className="w-auto p-3"
      trigger={
        <button
          type="button"
          aria-label="Escolher cor do evento"
          title="Cor do evento"
          className={cn(
            'size-9 shrink-0 rounded-full border border-input shadow-sm transition-transform hover:scale-105',
            'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-1',
            !value && 'bg-muted',
          )}
          style={value ? { backgroundColor: value } : undefined}
        />
      }
    >
      {(close) => (
        <div className="space-y-2">
          <div className="grid grid-cols-5 gap-2">
            {ENTRY_COLOR_PALETTE.map((color) => {
              const selected = value?.toLowerCase() === color;
              return (
                <button
                  key={color}
                  type="button"
                  aria-label={color}
                  aria-pressed={selected}
                  onClick={() => {
                    onChange(color);
                    close();
                  }}
                  className={cn(
                    'flex size-5 items-center justify-center rounded-full transition-transform hover:scale-110',
                    'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-1',
                    selected && 'ring-2 ring-ring ring-offset-1',
                  )}
                  style={{ backgroundColor: color }}
                >
                  {selected && <Check className="size-3 text-white" strokeWidth={3} />}
                </button>
              );
            })}
          </div>

          <button
            type="button"
            aria-pressed={value === null}
            onClick={() => {
              onChange(null);
              close();
            }}
            className={cn(
              'flex w-full items-center gap-2 rounded-md px-2 py-1.5 text-left text-sm transition-colors hover:bg-accent',
              value === null && 'font-medium',
            )}
          >
            <span className="flex size-5 items-center justify-center rounded-full border border-input bg-muted">
              {value === null && <Check className="size-3 text-foreground" strokeWidth={3} />}
            </span>
            Padrão (por tipo)
          </button>
        </div>
      )}
    </Popover>
  );
}
