import { useMemo } from 'react';
import { renderQrSvg } from '@/modules/labels/lib/qr-svg';
import type { LabelSpec } from '@/modules/labels/lib/label-model';

interface QrLabelProps {
  label: LabelSpec;
}

/** Human-facing tag distinguishing an item sticker from a location sticker at a glance. */
const KIND_TAG: Record<LabelSpec['kind'], string> = {
  item: 'Item',
  location: 'Local',
};

/**
 * A single printable QR sticker (card [E7] #92): the QR square on the left, the readable identification
 * on the right (bold title, secondary subtitle, and a small kind tag). Dumb and self-contained — it only
 * renders the {@link LabelSpec} it is given. `data-print-label` opts it out of being split across a page
 * break (see the print rules in index.css). Colours are pinned to black-on-white so the sticker stays
 * legible and scannable regardless of the app theme (labels are printed, never read on the dark UI).
 */
export function QrLabel({ label }: QrLabelProps) {
  // The SVG is derived purely from the payload, so it is memoized per label and never rebuilt on re-render.
  const svg = useMemo(() => renderQrSvg(label.payload), [label.payload]);

  return (
    <div
      data-print-label
      className="flex items-center gap-3 rounded-md border border-neutral-300 bg-white p-2 text-black"
    >
      <div
        className="size-20 shrink-0"
        aria-label={`QR ${label.title}`}
        // The SVG string is generated locally from a controlled payload (no user HTML), so injecting it is safe.
        dangerouslySetInnerHTML={{ __html: svg }}
      />
      <div className="min-w-0 flex-1">
        <span className="inline-block rounded-sm bg-neutral-100 px-1.5 py-0.5 text-[9px] font-semibold uppercase tracking-wide text-neutral-500">
          {KIND_TAG[label.kind]}
        </span>
        <p className="mt-1 truncate text-sm font-semibold leading-tight" title={label.title}>
          {label.title}
        </p>
        {label.subtitle ? (
          <p className="truncate text-xs text-neutral-500" title={label.subtitle}>
            {label.subtitle}
          </p>
        ) : null}
      </div>
    </div>
  );
}
