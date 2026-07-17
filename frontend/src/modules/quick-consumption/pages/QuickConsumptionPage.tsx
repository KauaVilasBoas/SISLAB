import { useCallback, useState, type ReactNode } from 'react';
import { AlertCircle, ArrowLeft, Loader2, QrCode } from 'lucide-react';
import { Button } from '@/shared/components/ui/button';
import { QrScanner } from '@/modules/quick-consumption/components/QrScanner';
import { ScanBanner } from '@/modules/quick-consumption/components/ScanBanner';
import { ItemCard } from '@/modules/quick-consumption/components/ItemCard';
import { ConsumptionForm } from '@/modules/quick-consumption/components/ConsumptionForm';
import { LocationItemPicker } from '@/modules/quick-consumption/components/LocationItemPicker';
import {
  useScannedLocation,
  useStockItemDetail,
} from '@/modules/quick-consumption/api/quick-consumption.queries';
import { resolveScannedQr, resolveStockItemId } from '@/modules/quick-consumption/lib/qr';

/**
 * A scanned SISLAB QR resolved to either a stock item (baixa directly) or a storage location (pick a
 * frasco from that cabinet, then baixa). `null` is the idle scanner screen.
 */
type Scan = { kind: 'item'; id: string } | { kind: 'location'; id: string } | null;

/**
 * Mobile quick-consumption "mother" screen (cards [E7] #63 / #92).
 *
 * Owns the whole flow state and composes dumb children. Two kinds of label converge on the same baixa
 * form:
 *  - `sislab:item:<guid>` (#63) resolves an item id, loads its card and shows the consumption form.
 *  - `sislab:location:<guid>` (#92) resolves a cabinet, lists the items it holds and lets the operator
 *    pick one — which then loads exactly like the item path.
 *
 * A confirmed consumption resets everything so the operator can scan the next label without leaving the
 * screen. It is a responsive SPA route (no PWA/service worker) constrained to a phone-width column.
 */
export function QuickConsumptionPage() {
  const [scan, setScan] = useState<Scan>(null);
  // When a location was scanned, the item chosen from its picker — drives the same card + form as #63.
  const [pickedItemId, setPickedItemId] = useState<string | null>(null);
  const [scanHint, setScanHint] = useState<string | null>(null);

  const location = useScannedLocation(scan?.kind === 'location' ? scan.id : null);

  // The item to load: a directly scanned item, or the one picked from a scanned location's list.
  const activeItemId = scan?.kind === 'item' ? scan.id : pickedItemId;
  const detail = useStockItemDetail(activeItemId);

  // Parse a scanned/typed payload. The camera path can carry either kind; the manual field (id/deep-link)
  // is treated as an item for backwards compatibility with #63. Unrecognized payloads keep the scanner up.
  const handleScan = useCallback((text: string) => {
    const resolved = resolveScannedQr(text) ?? asItemFallback(text);
    if (!resolved) {
      setScanHint('QR não reconhecido. Use uma etiqueta de item ou local do SISLAB.');
      return;
    }
    setScanHint(null);
    setPickedItemId(null);
    setScan(resolved);
  }, []);

  const resetFlow = useCallback(() => {
    setScan(null);
    setPickedItemId(null);
    setScanHint(null);
  }, []);

  // Back from an item's baixa form to the scanned location's item list (only meaningful in the location flow).
  const backToLocation = useCallback(() => setPickedItemId(null), []);

  // --- Idle: scanner ---------------------------------------------------------------------------------
  if (scan === null) {
    return (
      <Screen>
        <QrScanner active onScan={handleScan} hint={scanHint} />
      </Screen>
    );
  }

  // --- Location flow: pick an item from the cabinet, unless one is already picked --------------------
  if (scan.kind === 'location' && !pickedItemId) {
    return (
      <Screen>
        {location.isLoading ? (
          <LoadingCard label="Carregando local…" />
        ) : location.isError || !location.data?.found ? (
          <NotFoundCard
            message={location.data ? 'Local não encontrado para este QR.' : messageOf(location.error)}
            onBack={resetFlow}
          />
        ) : (
          <div className="space-y-4">
            <ScanBanner storageLocationName={location.data.name} kind="location" />
            <LocationItemPicker items={location.data.items} onPick={setPickedItemId} />
            <Button type="button" variant="ghost" className="w-full" onClick={resetFlow}>
              <ArrowLeft />
              Ler outro QR
            </Button>
          </div>
        )}
      </Screen>
    );
  }

  // --- Item flow (scanned item, or item picked from a location) -------------------------------------
  const fromLocation = scan.kind === 'location';
  const bannerName = fromLocation ? (location.data?.name ?? null) : (detail.data?.storageLocationName ?? null);

  return (
    <Screen>
      {detail.isLoading ? (
        <LoadingCard label="Carregando item…" />
      ) : detail.isError || !detail.data ? (
        <NotFoundCard
          message={messageOf(detail.error)}
          onBack={fromLocation ? backToLocation : resetFlow}
          backLabel={fromLocation ? 'Voltar aos itens do local' : 'Ler outro QR'}
        />
      ) : (
        <div className="space-y-4">
          <ScanBanner storageLocationName={bannerName} kind={fromLocation ? 'location' : 'item'} />
          <ItemCard item={detail.data} />
          <ConsumptionForm item={detail.data} onConfirmed={fromLocation ? backToLocation : resetFlow} />
          {fromLocation ? (
            <Button type="button" variant="ghost" className="w-full" onClick={backToLocation}>
              <ArrowLeft />
              Escolher outro item do local
            </Button>
          ) : null}
          <Button type="button" variant="ghost" className="w-full" onClick={resetFlow}>
            <ArrowLeft />
            Ler outro QR
          </Button>
        </div>
      )}
    </Screen>
  );
}

/** Treats a manually typed id/deep-link as an item scan (the keyboard fallback of #63). */
function asItemFallback(text: string): Scan {
  const id = resolveStockItemId(text);
  return id ? { kind: 'item', id } : null;
}

/** Phone-width column shell with the shared header, shared by every phase of the flow. */
function Screen({ children }: { children: ReactNode }) {
  return (
    <div className="mx-auto flex w-full max-w-md flex-col gap-4">
      <header className="flex items-center gap-2">
        <QrCode className="size-5 text-status-info" />
        <div>
          <h1 className="text-lg font-semibold leading-tight">Registro rápido</h1>
          <p className="text-xs text-muted-foreground">Baixa de consumo por QR</p>
        </div>
      </header>
      {children}
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

function LoadingCard({ label }: { label: string }) {
  return (
    <div className="flex flex-col items-center gap-2 rounded-xl border bg-card p-8 text-sm text-muted-foreground">
      <Loader2 className="size-6 animate-spin" />
      {label}
    </div>
  );
}

function NotFoundCard({
  message,
  onBack,
  backLabel = 'Ler outro QR',
}: {
  message?: string;
  onBack: () => void;
  backLabel?: string;
}) {
  return (
    <div className="flex flex-col items-center gap-3 rounded-xl border bg-card p-8 text-center">
      <AlertCircle className="size-8 text-destructive" />
      <p className="text-sm text-muted-foreground">{message ?? 'Nada encontrado para este QR.'}</p>
      <Button type="button" variant="outline" onClick={onBack}>
        <ArrowLeft />
        {backLabel}
      </Button>
    </div>
  );
}
