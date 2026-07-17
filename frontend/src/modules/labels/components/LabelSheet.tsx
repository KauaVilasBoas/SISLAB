import { QrLabel } from '@/modules/labels/components/QrLabel';
import type { LabelSpec } from '@/modules/labels/lib/label-model';

interface LabelSheetProps {
  labels: LabelSpec[];
}

/**
 * The printable sheet of QR labels (card [E7] #92). A responsive multi-column grid of {@link QrLabel}
 * stickers, flagged with `data-print-sheet` so the print rules in index.css reveal *only* this element
 * (hiding the app chrome) when the operator hits "Imprimir" — no server-side PDF, just the browser's
 * native print with `window.print()`. On screen it doubles as the live preview of what will come out of
 * the printer.
 *
 * The grid is fixed to two columns on paper (`print:grid-cols-2`) — a pragmatic fit for common A4 label
 * stock — while the on-screen preview flows from one to three columns with the viewport.
 */
export function LabelSheet({ labels }: LabelSheetProps) {
  return (
    <div
      data-print-sheet
      className="grid grid-cols-1 gap-2 rounded-lg border bg-white p-3 sm:grid-cols-2 lg:grid-cols-3 print:grid-cols-2 print:gap-2 print:border-0 print:p-0"
    >
      {labels.map((label) => (
        <QrLabel key={label.selectionKey} label={label} />
      ))}
    </div>
  );
}
