import { Sparkles } from 'lucide-react';

/**
 * A small, non-intrusive fixed pill marking the app as the public demo — so nobody mistakes the fictional
 * data for a live system (the pilot lab, LAFTE, is a real client). Rendered once at the App root in demo
 * mode, above every route. Bottom-right so it never collides with the sidebar or topbar.
 */
export function DemoRibbon() {
  return (
    <div
      role="status"
      className="pointer-events-none fixed bottom-3 right-3 z-[60] flex items-center gap-1.5 rounded-full border border-premium/30 bg-premium/15 px-3 py-1.5 text-xs font-medium text-premium shadow-sm backdrop-blur-sm"
    >
      <Sparkles className="size-3.5" />
      Demonstração · dados fictícios, somente leitura
    </div>
  );
}
