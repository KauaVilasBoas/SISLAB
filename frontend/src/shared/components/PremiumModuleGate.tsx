import type { ReactNode } from 'react';
import { Crown, LockKeyhole, Sparkles, Check, ArrowRight } from 'lucide-react';
import { Card } from '@/shared/components/ui/card';
import { Badge } from '@/shared/components/ui/badge';
import { Button } from '@/shared/components/ui/button';
import { cn } from '@/shared/lib/utils';

/**
 * The `inert` boolean HTML attribute (widely supported since 2023) is not yet in the installed
 * @types/react 18 JSX typings, so it is spread rather than written inline to keep `tsc` clean.
 */
const inertAttribute = { inert: '' } as Record<string, string>;

interface PremiumModuleGateProps {
  /** Module name shown in the showcase headline (e.g. "In vitro", "Biobanco"). */
  title: string;
  /** One or two sentences that sell the module — desire-building, not a spec sheet. */
  pitch: string;
  /** Optional feature bullets rendered as a checklist inside the card. */
  bullets?: readonly string[];
  /**
   * The real module's layout, rendered blurred behind the glass as a teaser of what's inside.
   * It is inert: aria-hidden, non-focusable and non-interactive (see the wrapper below). When
   * omitted a neutral gradient stands in.
   */
  preview?: ReactNode;
}

/**
 * Premium module showcase (immersive paywall). Renders the module's own preview blurred behind a
 * frosted-glass overlay, with a gold "Premium" card that generates desire for the upgrade. Used as
 * the route element for modules reserved for the Premium edition so that reaching them — by clicking
 * the sidebar or typing the URL — lands on this vitrine instead of the real screen (defense-in-depth,
 * same spirit as RequirePermissionRoute).
 *
 * The teaser behind the glass is decorative only: it is aria-hidden, has pointer-events disabled and
 * text selection off, and its focusable descendants are neutralised, so keyboard focus never leaks to
 * the frozen module and screen readers announce only the showcase card.
 */
export function PremiumModuleGate({
  title,
  pitch,
  bullets,
  preview,
}: PremiumModuleGateProps) {
  return (
    <div className="relative isolate min-h-[calc(100vh-8rem)] overflow-hidden rounded-xl border bg-card">
      {/* Frozen module preview — purely decorative backdrop. `inert` (spread because it predates the
          installed React 18 JSX typings) removes the whole subtree from the tab order and the
          accessibility tree, so focus can never land on the frozen module even if it had controls. */}
      <div
        aria-hidden
        tabIndex={-1}
        {...inertAttribute}
        className="pointer-events-none absolute inset-0 select-none blur-sm [&_*]:!cursor-default"
      >
        {preview ?? <DefaultPreview />}
      </div>

      {/* Frosted glass — dims and desaturates the teaser so the card floats above it. */}
      <div className="absolute inset-0 bg-gradient-to-b from-background/60 via-background/75 to-background/90 backdrop-blur-[2px]" />

      {/* Showcase card. */}
      <div className="relative z-10 flex min-h-[calc(100vh-8rem)] items-center justify-center p-6">
        <Card
          role="region"
          aria-label={`Módulo Premium: ${title}`}
          className="w-full max-w-lg overflow-hidden border-premium/30 bg-card/95 shadow-xl ring-1 ring-premium/10 backdrop-blur-sm"
        >
          {/* Gold hairline at the top edge — subtle "premium" signature. */}
          <div className="h-1 w-full bg-gradient-to-r from-transparent via-premium to-transparent" />

          <div className="flex flex-col items-center gap-5 px-8 py-9 text-center">
            {/* Crowned lock medallion. */}
            <div className="relative">
              <span
                aria-hidden
                className="flex size-16 items-center justify-center rounded-2xl bg-premium/12 ring-1 ring-inset ring-premium/25"
              >
                <LockKeyhole className="size-7 text-premium" strokeWidth={2.25} />
              </span>
              <span
                aria-hidden
                className="absolute -right-2 -top-2 flex size-7 items-center justify-center rounded-full bg-premium text-premium-foreground shadow-sm"
              >
                <Crown className="size-4" strokeWidth={2.5} />
              </span>
            </div>

            <div className="flex flex-wrap items-center justify-center gap-2">
              <Badge className="gap-1 border-transparent bg-premium/15 text-premium">
                <Sparkles className="size-3.5" />
                Premium
              </Badge>
              <Badge variant="muted" className="gap-1">
                Em breve
              </Badge>
            </div>

            <div className="space-y-1.5">
              <p className="text-xs font-semibold uppercase tracking-widest text-premium">
                {title}
              </p>
              <h2 className="text-2xl font-semibold tracking-tight text-foreground">
                Módulo Premium
              </h2>
              <p className="mx-auto max-w-md text-sm leading-relaxed text-muted-foreground">
                {pitch}
              </p>
            </div>

            {bullets && bullets.length > 0 && (
              <ul className="mx-auto flex max-w-sm flex-col gap-2 text-left">
                {bullets.map((bullet) => (
                  <li
                    key={bullet}
                    className="flex items-start gap-2.5 text-sm text-foreground/90"
                  >
                    <span
                      aria-hidden
                      className="mt-0.5 flex size-4 shrink-0 items-center justify-center rounded-full bg-premium/15"
                    >
                      <Check className="size-3 text-premium" strokeWidth={3} />
                    </span>
                    {bullet}
                  </li>
                ))}
              </ul>
            )}

            <div className="mt-1 flex w-full flex-col items-center gap-3">
              <Button
                type="button"
                disabled
                aria-disabled
                title="Disponível na versão Premium do SISLAB"
                className={cn(
                  'w-full max-w-xs gap-2 border-transparent',
                  'bg-premium text-premium-foreground shadow-sm',
                  // The disabled state dims the button; keep the gold readable while signalling it is inert.
                  'disabled:opacity-90',
                )}
              >
                <Sparkles className="size-4" />
                Fazer upgrade
                <ArrowRight className="size-4" />
              </Button>
              <p className="text-xs text-muted-foreground">
                Disponível na versão Premium do SISLAB.
              </p>
            </div>
          </div>
        </Card>
      </div>
    </div>
  );
}

/** Neutral placeholder backdrop when a module doesn't supply its own preview. */
function DefaultPreview() {
  return (
    <div className="space-y-6 p-6">
      <div className="h-8 w-64 rounded-md bg-muted" />
      <div className="grid gap-4 sm:grid-cols-3">
        {Array.from({ length: 3 }).map((_, i) => (
          <div key={i} className="h-24 rounded-lg border bg-card" />
        ))}
      </div>
      <div className="space-y-3 rounded-lg border bg-card p-6">
        {Array.from({ length: 6 }).map((_, i) => (
          <div key={i} className="h-6 w-full rounded bg-muted" />
        ))}
      </div>
    </div>
  );
}
