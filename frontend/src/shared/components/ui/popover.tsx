import { useEffect, useRef, useState, type ReactNode } from 'react';
import { cn } from '@/shared/lib/utils';

interface PopoverProps {
  /** The clickable anchor (e.g. the bell button). Receives no props — wrap your own trigger. */
  trigger: ReactNode;
  /** Panel body, rendered only while open. Receives a `close` callback to dismiss from inside. */
  children: (close: () => void) => ReactNode;
  /** Accessible label for the panel region. */
  label: string;
  /** Horizontal alignment of the panel against the trigger. Defaults to right-aligned. */
  align?: 'start' | 'end';
  /** Extra classes for the floating panel (width, etc.). */
  className?: string;
}

/**
 * Lightweight anchored popover (no @radix-ui/react-popover — the app installs neither, matching the in-house
 * <Modal>). The trigger and panel share a `relative` wrapper so the panel floats right under the anchor; it
 * closes on outside pointerdown and on Escape, and locks focus to nothing (it is a transient panel, not a
 * dialog). Sufficient for the notification center; a full a11y popover can replace it later.
 */
export function Popover({
  trigger,
  children,
  label,
  align = 'end',
  className,
}: PopoverProps) {
  const [open, setOpen] = useState(false);
  const rootRef = useRef<HTMLDivElement>(null);

  const close = () => setOpen(false);

  useEffect(() => {
    if (!open) return;

    const onPointerDown = (event: PointerEvent) => {
      if (rootRef.current && !rootRef.current.contains(event.target as Node)) {
        setOpen(false);
      }
    };
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') setOpen(false);
    };

    document.addEventListener('pointerdown', onPointerDown);
    document.addEventListener('keydown', onKeyDown);
    return () => {
      document.removeEventListener('pointerdown', onPointerDown);
      document.removeEventListener('keydown', onKeyDown);
    };
  }, [open]);

  return (
    <div ref={rootRef} className="relative">
      <div onClick={() => setOpen((prev) => !prev)}>{trigger}</div>

      {open ? (
        <div
          role="dialog"
          aria-label={label}
          className={cn(
            'absolute z-50 mt-2 rounded-xl border bg-card text-card-foreground shadow-lg',
            align === 'end' ? 'right-0' : 'left-0',
            className,
          )}
        >
          {children(close)}
        </div>
      ) : null}
    </div>
  );
}
