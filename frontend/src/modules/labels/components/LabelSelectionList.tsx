import { CheckSquare, Square } from 'lucide-react';
import { Button } from '@/shared/components/ui/button';
import { cn } from '@/shared/lib/utils';
import type { LabelSpec } from '@/modules/labels/lib/label-model';

interface LabelSelectionListProps {
  /** Every candidate label the current source/filter yields. */
  candidates: LabelSpec[];
  /** Selection keys currently checked. */
  selected: ReadonlySet<string>;
  /** Toggles one candidate's membership in the selection. */
  onToggle: (selectionKey: string) => void;
  /** Selects / clears every visible candidate at once. */
  onSelectAll: () => void;
  onClear: () => void;
  /** Loading / empty affordances owned by the parent so this list stays dumb. */
  isLoading?: boolean;
  emptyMessage?: string;
}

/**
 * Dumb multi-select list of label candidates (card [E7] #92). Each row is a checkbox plus the readable
 * title/subtitle; the header carries the running count and the "select all / clear" shortcuts. Whether
 * the candidates are items or locations is the parent's concern — this component only knows
 * {@link LabelSpec}, which keeps the two sources visually and behaviourally identical.
 */
export function LabelSelectionList({
  candidates,
  selected,
  onToggle,
  onSelectAll,
  onClear,
  isLoading = false,
  emptyMessage = 'Nada para etiquetar aqui.',
}: LabelSelectionListProps) {
  const allSelected = candidates.length > 0 && candidates.every((c) => selected.has(c.selectionKey));

  return (
    <div className="flex flex-col rounded-lg border">
      <div className="flex items-center justify-between gap-2 border-b bg-muted/30 px-3 py-2">
        <span className="text-sm text-muted-foreground">
          {selected.size} de {candidates.length} selecionado{selected.size === 1 ? '' : 's'}
        </span>
        <div className="flex gap-1">
          <Button
            type="button"
            variant="ghost"
            size="sm"
            onClick={allSelected ? onClear : onSelectAll}
            disabled={candidates.length === 0}
          >
            {allSelected ? <Square /> : <CheckSquare />}
            {allSelected ? 'Limpar' : 'Selecionar todos'}
          </Button>
        </div>
      </div>

      <div className="max-h-96 overflow-y-auto">
        {isLoading ? (
          <p className="px-3 py-6 text-center text-sm text-muted-foreground">Carregando…</p>
        ) : candidates.length === 0 ? (
          <p className="px-3 py-6 text-center text-sm text-muted-foreground">{emptyMessage}</p>
        ) : (
          <ul>
            {candidates.map((candidate) => {
              const checked = selected.has(candidate.selectionKey);
              return (
                <li key={candidate.selectionKey}>
                  <label
                    className={cn(
                      'flex cursor-pointer items-center gap-3 border-b px-3 py-2 text-sm transition-colors last:border-b-0 hover:bg-muted/40',
                      checked && 'bg-muted/30',
                    )}
                  >
                    <input
                      type="checkbox"
                      checked={checked}
                      onChange={() => onToggle(candidate.selectionKey)}
                      className="size-4 rounded border-input"
                    />
                    <span className="min-w-0 flex-1">
                      <span className="block truncate font-medium">{candidate.title}</span>
                      {candidate.subtitle ? (
                        <span className="block truncate text-xs text-muted-foreground">
                          {candidate.subtitle}
                        </span>
                      ) : null}
                    </span>
                  </label>
                </li>
              );
            })}
          </ul>
        )}
      </div>
    </div>
  );
}
