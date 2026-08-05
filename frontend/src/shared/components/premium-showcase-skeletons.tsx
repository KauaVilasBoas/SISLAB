import type { ReactNode } from 'react';

/**
 * Presentational skeleton primitives shared by the Premium showcases (Experiments, Audit, …).
 *
 * Muted, side-effect-free blocks that read as a frozen module behind the PremiumModuleGate blur — no
 * data fetch, no loading/empty states. Kept in `shared/` (transversal) so each module's showcase can
 * reuse them without importing across module boundaries.
 */

/** Header row: a title + subtitle on the left and a muted "primary action" block on the right. */
export function SkelHeader({ title, subtitle }: { title: string; subtitle: string }) {
  return (
    <div className="flex items-center justify-between border-b pb-4">
      <div className="space-y-2">
        <div className="text-2xl font-semibold tracking-tight text-foreground/80">
          {title}
        </div>
        <div className="text-sm text-muted-foreground">{subtitle}</div>
      </div>
      <div className="h-9 w-40 rounded-md bg-primary/70" />
    </div>
  );
}

/** A generic table: a header row of column labels over `rows` muted cell rows. */
export function SkelTable({ columns, rows }: { columns: string[]; rows: number }) {
  return (
    <div className="rounded-lg border bg-card">
      <div
        className="grid gap-4 border-b px-4 py-3 text-xs uppercase tracking-wide text-muted-foreground"
        style={{ gridTemplateColumns: `repeat(${columns.length}, minmax(0, 1fr))` }}
      >
        {columns.map((column) => (
          <span key={column}>{column}</span>
        ))}
      </div>
      {Array.from({ length: rows }).map((_, rowIndex) => (
        <div
          key={rowIndex}
          className="grid gap-4 border-b px-4 py-4 last:border-0"
          style={{ gridTemplateColumns: `repeat(${columns.length}, minmax(0, 1fr))` }}
        >
          {columns.map((column, colIndex) => (
            <div
              key={column}
              className="h-4 rounded bg-muted"
              style={{ width: `${[85, 55, 70, 45, 60, 65][colIndex] ?? 60}%` }}
            />
          ))}
        </div>
      ))}
    </div>
  );
}

/** Padded vertical stack — the outer frame every showcase preview sits in. */
export function ShowcaseShell({ children }: { children: ReactNode }) {
  return <div className="space-y-6 p-6">{children}</div>;
}
