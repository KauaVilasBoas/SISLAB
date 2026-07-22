import { type ReactNode } from 'react';
import { cn } from '@/shared/lib/utils';

interface TooltipProps {
  content: string;
  children: ReactNode;
  side?: 'top' | 'bottom' | 'left' | 'right';
  className?: string;
}

const SIDE_CLASSES: Record<NonNullable<TooltipProps['side']>, string> = {
  top: 'bottom-full left-1/2 mb-2 -translate-x-1/2',
  bottom: 'top-full left-1/2 mt-2 -translate-x-1/2',
  left: 'right-full top-1/2 mr-2 -translate-y-1/2',
  right: 'left-full top-1/2 ml-2 -translate-y-1/2',
};

export function Tooltip({ content, children, side = 'top', className }: TooltipProps) {
  return (
    <div className="group relative inline-flex">
      {children}
      <div
        role="tooltip"
        className={cn(
          'pointer-events-none absolute z-50 w-max max-w-[220px] rounded-md bg-popover px-3 py-1.5',
          'text-xs text-popover-foreground shadow-md',
          'opacity-0 transition-opacity duration-150 group-hover:opacity-100',
          SIDE_CLASSES[side],
          className,
        )}
      >
        {content}
      </div>
    </div>
  );
}
