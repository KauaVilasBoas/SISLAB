import { Modal } from '@/shared/components/ui/modal';
import { Button } from '@/shared/components/ui/button';
import type { EditScope } from '@/modules/agenda/types';

/**
 * "Edit recurring event?" chooser (card [E10.6]) — the Google-Calendar three-way scope prompt shown before
 * saving an edit to a recurring entry. One-off entries skip this entirely (the form saves directly).
 */
interface EditRecurringDialogProps {
  open: boolean;
  onClose: () => void;
  onChoose: (scope: EditScope) => void;
}

const OPTIONS: { scope: EditScope; label: string }[] = [
  { scope: 'OnlyThis', label: 'Apenas esta ocorrência' },
  { scope: 'ThisAndFollowing', label: 'Esta e as seguintes' },
  { scope: 'AllOccurrences', label: 'Todas as ocorrências' },
];

export function EditRecurringDialog({ open, onClose, onChoose }: EditRecurringDialogProps) {
  return (
    <Modal
      open={open}
      onClose={onClose}
      title="Editar evento recorrente"
      footer={
        <Button variant="ghost" onClick={onClose}>
          Cancelar
        </Button>
      }
    >
      <div className="space-y-2">
        {OPTIONS.map((option) => (
          <button
            key={option.scope}
            type="button"
            onClick={() => onChoose(option.scope)}
            className="flex w-full items-center rounded-md border px-3 py-2.5 text-left text-sm transition-colors hover:bg-accent"
          >
            {option.label}
          </button>
        ))}
      </div>
    </Modal>
  );
}
