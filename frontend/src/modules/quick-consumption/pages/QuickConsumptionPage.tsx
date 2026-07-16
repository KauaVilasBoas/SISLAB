import { useCallback, useState } from 'react';
import { AlertCircle, ArrowLeft, Loader2, QrCode } from 'lucide-react';
import { Button } from '@/shared/components/ui/button';
import { QrScanner } from '@/modules/quick-consumption/components/QrScanner';
import { ScanBanner } from '@/modules/quick-consumption/components/ScanBanner';
import { ItemCard } from '@/modules/quick-consumption/components/ItemCard';
import { ConsumptionForm } from '@/modules/quick-consumption/components/ConsumptionForm';
import { useStockItemDetail } from '@/modules/quick-consumption/api/quick-consumption.queries';
import { resolveStockItemId } from '@/modules/quick-consumption/lib/qr';

/**
 * Mobile quick-consumption "mother" screen (card [E7] #63).
 *
 * Owns the whole flow state and composes dumb children: the QR scanner, the "QR lido" banner, the item
 * card and the consumption form. Scanning `sislab:item:<guid>` resolves an id, the id drives the
 * item-detail query, and a confirmed consumption resets everything so the operator can scan the next
 * label without leaving the screen. It is a responsive SPA route (no PWA/service worker), so it lives in
 * the authenticated shell but constrains itself to a phone-width column.
 *
 * Two phases: (1) scanning — camera preview with a keyboard fallback; (2) item loaded — banner + card +
 * form. An unrecognized QR keeps the scanner up with a friendly hint; a valid QR for an unknown/foreign
 * item shows a not-found state with a "scan again" action.
 */
export function QuickConsumptionPage() {
  const [stockItemId, setStockItemId] = useState<string | null>(null);
  const [scanHint, setScanHint] = useState<string | null>(null);

  const detail = useStockItemDetail(stockItemId);

  // Parse a scanned/typed payload into an id. Unrecognized payloads keep the scanner up with a hint.
  const handleScan = useCallback((text: string) => {
    const id = resolveStockItemId(text);
    if (!id) {
      setScanHint('QR não reconhecido. Use uma etiqueta de item do SISLAB.');
      return;
    }
    setScanHint(null);
    setStockItemId(id);
  }, []);

  const resetFlow = useCallback(() => {
    setStockItemId(null);
    setScanHint(null);
  }, []);

  return (
    <div className="mx-auto flex w-full max-w-md flex-col gap-4">
      <header className="flex items-center gap-2">
        <QrCode className="size-5 text-status-info" />
        <div>
          <h1 className="text-lg font-semibold leading-tight">Registro rápido</h1>
          <p className="text-xs text-muted-foreground">Baixa de consumo por QR</p>
        </div>
      </header>

      {stockItemId === null ? (
        <QrScanner active onScan={handleScan} hint={scanHint} />
      ) : detail.isLoading ? (
        <LoadingItem />
      ) : detail.isError || !detail.data ? (
        <ItemNotFound message={messageOf(detail.error)} onBack={resetFlow} />
      ) : (
        <div className="space-y-4">
          <ScanBanner storageLocationName={detail.data.storageLocationName} />
          <ItemCard item={detail.data} />
          <ConsumptionForm item={detail.data} onConfirmed={resetFlow} />
          <Button type="button" variant="ghost" className="w-full" onClick={resetFlow}>
            <ArrowLeft />
            Ler outro QR
          </Button>
        </div>
      )}
    </div>
  );
}

/** Safely reads a message off a rejected query error (our http layer rejects with a `{ message }` shape). */
function messageOf(error: unknown): string | undefined {
  if (error && typeof error === 'object' && 'message' in error) {
    const message = (error as { message?: unknown }).message;
    if (typeof message === 'string') return message;
  }
  return undefined;
}

function LoadingItem() {
  return (
    <div className="flex flex-col items-center gap-2 rounded-xl border bg-card p-8 text-sm text-muted-foreground">
      <Loader2 className="size-6 animate-spin" />
      Carregando item…
    </div>
  );
}

function ItemNotFound({ message, onBack }: { message?: string; onBack: () => void }) {
  return (
    <div className="flex flex-col items-center gap-3 rounded-xl border bg-card p-8 text-center">
      <AlertCircle className="size-8 text-destructive" />
      <p className="text-sm text-muted-foreground">
        {message ?? 'Item não encontrado para este QR.'}
      </p>
      <Button type="button" variant="outline" onClick={onBack}>
        <ArrowLeft />
        Ler outro QR
      </Button>
    </div>
  );
}
