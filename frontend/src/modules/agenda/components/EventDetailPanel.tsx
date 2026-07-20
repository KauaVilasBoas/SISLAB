import { CalendarClock, Pencil, Repeat, Trash2, X } from 'lucide-react';
import { Button } from '@/shared/components/ui/button';
import { cn } from '@/shared/lib/utils';
import { ACTIVITY_TYPE_COLOR, ACTIVITY_TYPE_LABEL } from '@/modules/agenda/presentation';
import { localTime } from '@/modules/agenda/lib/calendar';
import type { CalendarItem } from '@/modules/agenda/types';

/**
 * Right-hand detail panel for a clicked calendar occurrence (card [E10.5]). Shows the occurrence's essentials
 * and offers edit / delete. Deleting a recurring occurrence cancels just that instance (EXDATE); deleting a
 * one-off removes the entry.
 */
interface EventDetailPanelProps {
  item: CalendarItem;
  onClose: () => void;
  onEdit: () => void;
  onDelete: () => void;
}

export function EventDetailPanel({ item, onClose, onEdit, onDelete }: EventDetailPanelProps) {
  const color = ACTIVITY_TYPE_COLOR[item.activityType];

  return (
    <aside className="w-full max-w-sm shrink-0 rounded-lg border bg-card p-5">
      <div className="flex items-start justify-between gap-2">
        <span
          className={cn(
            'inline-flex items-center gap-1.5 rounded-full border px-2 py-0.5 text-xs font-medium',
            color.bg,
            color.border,
            color.text,
          )}
        >
          <span className={cn('size-2 rounded-full', color.dot)} />
          {ACTIVITY_TYPE_LABEL[item.activityType]}
        </span>
        <button
          type="button"
          aria-label="Fechar detalhes"
          onClick={onClose}
          className="rounded-md p-1 text-muted-foreground hover:bg-accent hover:text-accent-foreground"
        >
          <X className="size-4" />
        </button>
      </div>

      <h3 className="mt-3 text-lg font-semibold leading-tight">{item.title}</h3>

      <dl className="mt-4 space-y-2 text-sm">
        <div className="flex items-center gap-2 text-muted-foreground">
          <CalendarClock className="size-4 shrink-0" />
          <span>
            {item.isAllDay
              ? 'Dia inteiro'
              : `${localTime(item.startDateUtc)} – ${localTime(item.endDateUtc)}`}
          </span>
        </div>

        {item.isRecurring && (
          <div className="flex items-center gap-2 text-muted-foreground">
            <Repeat className="size-4 shrink-0" />
            <span>Evento recorrente</span>
          </div>
        )}

        {item.experimentName && (
          <div className="text-muted-foreground">
            Experimento: <span className="text-foreground">{item.experimentName}</span>
          </div>
        )}
      </dl>

      <div className="mt-6 flex gap-2">
        <Button variant="outline" size="sm" onClick={onEdit} className="flex-1">
          <Pencil className="size-4" /> Editar
        </Button>
        <Button variant="destructive" size="sm" onClick={onDelete} className="flex-1">
          <Trash2 className="size-4" /> Excluir
        </Button>
      </div>
    </aside>
  );
}
