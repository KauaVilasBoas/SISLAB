import { MapPin } from 'lucide-react';

interface ScanBannerProps {
  /** Storage location name of the scanned item — the "armário / local" the label lives in. */
  storageLocationName: string | null;
}

/**
 * "QR lido — <armário> / <local>" confirmation banner (card [E7] #63). The location comes from the
 * scanned item's detail (there is no cabinet QR yet — deferred to #92), so it reads the item's storage
 * location, falling back to a neutral label when the item has none named.
 */
export function ScanBanner({ storageLocationName }: ScanBannerProps) {
  return (
    <div className="flex items-center gap-2 rounded-lg border border-status-info/30 bg-status-info/10 px-3 py-2 text-sm">
      <MapPin className="size-4 shrink-0 text-status-info" />
      <span className="min-w-0">
        <span className="font-medium">QR lido</span>
        <span className="text-muted-foreground">
          {' — '}
          {storageLocationName?.trim() || 'Local não informado'}
        </span>
      </span>
    </div>
  );
}
