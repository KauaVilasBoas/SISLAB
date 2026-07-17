import * as React from 'react';
import { cn } from '@/shared/lib/utils';

/**
 * Native <select> styled to match the shadcn Input primitive (the app depends on neither
 * @radix-ui/react-select nor a shadcn Select component). Shared by the inventory forms so the
 * category/location/unit dropdowns stay visually consistent with the rest of the design system.
 */
export const Select = React.forwardRef<HTMLSelectElement, React.ComponentProps<'select'>>(
  ({ className, ...props }, ref) => (
    <select
      ref={ref}
      className={cn(
        'flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-sm transition-colors focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring disabled:cursor-not-allowed disabled:opacity-50',
        className,
      )}
      {...props}
    />
  ),
);
Select.displayName = 'Select';

/** A labelled form field wrapper: the vertical Label + control stack the forms repeat. */
export function Field({
  label,
  htmlFor,
  children,
  className,
}: {
  label: string;
  htmlFor: string;
  children: React.ReactNode;
  className?: string;
}) {
  return (
    <div className={cn('flex flex-col gap-2', className)}>
      <label htmlFor={htmlFor} className="text-sm font-medium leading-none">
        {label}
      </label>
      {children}
    </div>
  );
}
