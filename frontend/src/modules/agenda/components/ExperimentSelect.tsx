import { useState } from 'react';
import { Check, ChevronsUpDown, Loader2, X } from 'lucide-react';
import { Label } from '@/shared/components/ui/label';
import { Input } from '@/shared/components/ui/input';
import { cn } from '@/shared/lib/utils';
import { useExperimentOptions } from '@/modules/agenda/api/entries.queries';

/**
 * Nullable experiment autocomplete for the entry form (card [E10.6]). Debounce-free (the option list is a small
 * client-filtered page); an entry may have no experiment, so the selection is clearable.
 */
interface ExperimentSelectProps {
  value: string | null;
  valueLabel: string | null;
  onChange: (id: string | null, label: string | null) => void;
}

export function ExperimentSelect({ value, valueLabel, onChange }: ExperimentSelectProps) {
  const [open, setOpen] = useState(false);
  const [search, setSearch] = useState('');
  const { data: options = [], isFetching } = useExperimentOptions(search);

  return (
    <div className="space-y-1.5">
      <Label>Experimento (opcional)</Label>

      {value ? (
        <div className="flex items-center justify-between gap-2 rounded-md border border-input px-3 py-2 text-sm">
          <span className="truncate">{valueLabel ?? value}</span>
          <button
            type="button"
            aria-label="Remover experimento"
            onClick={() => onChange(null, null)}
            className="rounded p-0.5 text-muted-foreground hover:bg-accent hover:text-accent-foreground"
          >
            <X className="size-4" />
          </button>
        </div>
      ) : (
        <div className="relative">
          <button
            type="button"
            onClick={() => setOpen((o) => !o)}
            className="flex h-9 w-full items-center justify-between rounded-md border border-input bg-transparent px-3 py-1 text-sm text-muted-foreground shadow-sm focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
          >
            Vincular experimento…
            <ChevronsUpDown className="size-4 opacity-50" />
          </button>

          {open && (
            <div className="absolute z-10 mt-1 w-full rounded-md border bg-popover p-1 shadow-md">
              <div className="p-1">
                <Input
                  autoFocus
                  placeholder="Buscar…"
                  value={search}
                  onChange={(e) => setSearch(e.target.value)}
                />
              </div>
              <div className="max-h-48 overflow-y-auto">
                {isFetching ? (
                  <div className="flex items-center gap-2 px-2 py-3 text-sm text-muted-foreground">
                    <Loader2 className="size-4 animate-spin" /> Carregando…
                  </div>
                ) : options.length === 0 ? (
                  <p className="px-2 py-3 text-sm text-muted-foreground">Nenhum experimento.</p>
                ) : (
                  options.map((option) => (
                    <button
                      key={option.id}
                      type="button"
                      onClick={() => {
                        onChange(option.id, option.title);
                        setOpen(false);
                        setSearch('');
                      }}
                      className={cn(
                        'flex w-full items-center gap-2 rounded px-2 py-1.5 text-left text-sm hover:bg-accent',
                      )}
                    >
                      <Check className="size-4 opacity-0" />
                      <span className="truncate">{option.title}</span>
                    </button>
                  ))
                )}
              </div>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
