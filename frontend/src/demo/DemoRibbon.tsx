import { Code2, Info, Sparkles } from 'lucide-react';

/**
 * A small, non-intrusive fixed marker for the public demo, so nobody mistakes the fictional data for a
 * live system (the pilot lab, LAFTE, is a real client). Rendered once at the App root in demo mode, above
 * every route. Bottom-right so it never collides with the sidebar or topbar. Besides the "about the demo"
 * pill it exposes a persistent, discreet "Como foi construído" entry for the technical audience: the same
 * surface offered inside the welcome dialog, now reachable at any moment.
 */
export function DemoRibbon({
  onOpenIntro,
  onOpenHowItsBuilt,
  hidden = false,
}: {
  onOpenIntro: () => void;
  onOpenHowItsBuilt: () => void;
  /** Recolhe a ribbon enquanto o tour guiado está ativo, para não brigar com o card do tour no canto. */
  hidden?: boolean;
}) {
  if (hidden) return null;

  return (
    <div className="fixed bottom-3 right-3 z-40 flex items-center gap-2">
      <button
        type="button"
        onClick={onOpenHowItsBuilt}
        title="Como o SISLAB foi construído"
        className="flex items-center gap-1.5 rounded-full border bg-card/90 px-3 py-1.5 text-xs font-medium text-muted-foreground shadow-sm backdrop-blur-sm transition-colors hover:bg-accent hover:text-accent-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
      >
        <Code2 className="size-3.5" aria-hidden />
        Como foi construído
      </button>

      <button
        type="button"
        onClick={onOpenIntro}
        title="Sobre esta demonstração"
        className="flex items-center gap-1.5 rounded-full border border-premium/30 bg-premium/15 px-3 py-1.5 text-xs font-medium text-premium shadow-sm backdrop-blur-sm transition-colors hover:bg-premium/25 focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-premium"
      >
        <Sparkles className="size-3.5" aria-hidden />
        Demonstração · dados fictícios, somente leitura
        <Info className="size-3.5 opacity-70" aria-hidden />
      </button>
    </div>
  );
}
