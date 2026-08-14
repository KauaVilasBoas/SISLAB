import { Info, Sparkles } from 'lucide-react';

/**
 * A small, non-intrusive fixed pill marking the app as the public demo — so nobody mistakes the fictional
 * data for a live system (the pilot lab, LAFTE, is a real client). Rendered once at the App root in demo
 * mode, above every route. Bottom-right so it never collides with the sidebar or topbar.
 */
export function DemoRibbon({ onOpenIntro }: { onOpenIntro: () => void }) {
  return (
    <button
      type="button"
      onClick={onOpenIntro}
      title="Sobre esta demonstração"
      className="fixed bottom-3 right-3 z-40 flex items-center gap-1.5 rounded-full border border-premium/30 bg-premium/15 px-3 py-1.5 text-xs font-medium text-premium shadow-sm backdrop-blur-sm transition-colors hover:bg-premium/25 focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-premium"
    >
      <Sparkles className="size-3.5" aria-hidden />
      Demonstração · dados fictícios, somente leitura
      <Info className="size-3.5 opacity-70" aria-hidden />
    </button>
  );
}
