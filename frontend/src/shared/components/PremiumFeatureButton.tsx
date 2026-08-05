import type { ReactNode } from 'react';
import { ArrowRight, Check, Crown, LockKeyhole, Sparkles } from 'lucide-react';
import { Popover } from '@/shared/components/ui/popover';
import { Badge } from '@/shared/components/ui/badge';
import { Button } from '@/shared/components/ui/button';
import { cn } from '@/shared/lib/utils';

interface PremiumFeatureButtonProps {
  /** Button label (e.g. "Exportar para o calendário"). */
  label: string;
  /** Optional leading icon for the button. */
  icon?: ReactNode;
  /** Feature name shown as the eyebrow inside the popover. */
  feature: string;
  /** One or two sentences that sell the feature — desire-building, not a spec sheet. */
  pitch: string;
  /** Optional feature bullets rendered as a checklist. */
  bullets?: readonly string[];
  /** Horizontal alignment of the popover against the trigger. Defaults to right-aligned. */
  align?: 'start' | 'end';
}

/**
 * Inline "Premium" affordance for a single feature — as opposed to a whole route/module, which uses
 * {@link PremiumModuleGate}. Renders a locked, gold-tinted button that opens a compact premium pitch
 * popover instead of performing the action (e.g. the calendar/iCal export on the Agenda screen). The
 * action never fires: this is a teaser that builds expectation for the Premium edition.
 */
export function PremiumFeatureButton({
  label,
  icon,
  feature,
  pitch,
  bullets,
  align = 'end',
}: PremiumFeatureButtonProps) {
  return (
    <Popover
      align={align}
      label={`${feature} — recurso Premium`}
      className="w-80"
      trigger={
        <Button
          type="button"
          variant="outline"
          className="gap-2 border-premium/30 text-foreground hover:bg-premium/10"
        >
          {icon}
          {label}
          <LockKeyhole className="text-premium" />
        </Button>
      }
    >
      {() => (
        <div className="overflow-hidden rounded-xl">
          {/* Gold hairline — the same "premium" signature as the module gate. */}
          <div className="h-1 w-full bg-gradient-to-r from-transparent via-premium to-transparent" />
          <div className="space-y-4 p-5">
            <div className="flex items-center gap-3">
              <span
                aria-hidden
                className="relative flex size-10 shrink-0 items-center justify-center rounded-xl bg-premium/12 ring-1 ring-inset ring-premium/25"
              >
                <LockKeyhole className="size-5 text-premium" strokeWidth={2.25} />
                <span className="absolute -right-1.5 -top-1.5 flex size-5 items-center justify-center rounded-full bg-premium text-premium-foreground shadow-sm">
                  <Crown className="size-3" strokeWidth={2.5} />
                </span>
              </span>
              <div className="min-w-0">
                <div className="flex flex-wrap items-center gap-1.5">
                  <Badge className="gap-1 border-transparent bg-premium/15 text-premium">
                    <Sparkles className="size-3" />
                    Premium
                  </Badge>
                  <Badge variant="muted">Em breve</Badge>
                </div>
                <p className="mt-1 truncate text-[11px] font-semibold uppercase tracking-widest text-premium">
                  {feature}
                </p>
              </div>
            </div>

            <p className="text-sm leading-relaxed text-muted-foreground">{pitch}</p>

            {bullets && bullets.length > 0 && (
              <ul className="flex flex-col gap-1.5 text-left">
                {bullets.map((bullet) => (
                  <li
                    key={bullet}
                    className="flex items-start gap-2 text-sm text-foreground/90"
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

            <Button
              type="button"
              disabled
              aria-disabled
              title="Disponível na versão Premium do SISLAB"
              className={cn(
                'w-full gap-2 border-transparent bg-premium text-premium-foreground shadow-sm',
                'disabled:opacity-90',
              )}
            >
              <Sparkles className="size-4" />
              Fazer upgrade
              <ArrowRight className="size-4" />
            </Button>
          </div>
        </div>
      )}
    </Popover>
  );
}
