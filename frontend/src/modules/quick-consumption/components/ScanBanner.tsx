import { MapPin } from 'lucide-react';

interface ScanBannerProps {
  /** Storage location name of the scan — the "armário / local" the label lives in (item QR) or points at (location QR). */
  storageLocationName: string | null;
  /**
   * What was scanned, so the banner reads naturally for both entry points (card [E7] #63 item QR and
   * #92 location QR). `item` (default) keeps the original "QR lido — <local do item>"; `location` reads
   * "Local lido — <armário>" once the operator has scanned a cabinet.
   */
  kind?: 'item' | 'location';
}

/**
 * "QR lido — <armário> / <local>" confirmation banner. For an item QR (#63) the location comes from the
 * scanned item's detail; for a location QR (#92) it is the scanned cabinet itself. Falls back to a
 * neutral label when the location has no name.
 */
export function ScanBanner({ storageLocationName, kind = 'item' }: ScanBannerProps) {
  const heading = kind === 'location' ? 'Local lido' : 'QR lido';

  return (
    <div className="flex items-center gap-2 rounded-lg border border-status-info/30 bg-status-info/10 px-3 py-2 text-sm">
      <MapPin className="size-4 shrink-0 text-status-info" />
      <span className="min-w-0">
        <span className="font-medium">{heading}</span>
        <span className="text-muted-foreground">
          {' — '}
          {storageLocationName?.trim() || 'Local não informado'}
        </span>
      </span>
    </div>
  );
}
