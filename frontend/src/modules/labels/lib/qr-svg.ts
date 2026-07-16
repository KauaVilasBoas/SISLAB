import { create } from 'qrcode';

/**
 * Renders a QR payload to a self-contained, crisp SVG string (card [E7] #92).
 *
 * We build the SVG ourselves from the module matrix (`QRCode.create`, fully synchronous) rather than
 * awaiting `QRCode.toString` per label: a printable sheet renders dozens of codes at once, so a
 * synchronous path avoids a flash of empty labels and any per-code Promise churn. Vector output (not a
 * canvas/PNG) is deliberate — a QR is pure black-and-white geometry that must stay razor-sharp at any
 * printer DPI and at any physical sticker size, which only SVG guarantees.
 *
 * The matrix is emitted as a single `<path>` of 1×1 module squares (compact markup, one fill), on a
 * `0 0 size size` viewBox so the consumer scales it with CSS. A 4-module quiet zone (the QR spec's
 * minimum) is baked in so scanners lock on even when the sticker sits flush against cabinet edges.
 * Error-correction level M balances density against smudge/tear tolerance on a lab bench.
 */

const QUIET_ZONE = 4;

/** Builds the `<path d="…">` data for the dark modules, offset by the quiet zone. */
function toModulePath(get: (row: number, col: number) => number, size: number): string {
  const segments: string[] = [];
  for (let row = 0; row < size; row++) {
    for (let col = 0; col < size; col++) {
      if (get(row, col)) {
        segments.push(`M${col + QUIET_ZONE} ${row + QUIET_ZONE}h1v1h-1z`);
      }
    }
  }
  return segments.join('');
}

/**
 * Returns an SVG string encoding <paramref name="payload"/> as a QR code. The `<rect>` paints the quiet
 * zone white so the code reads on any label background; `shape-rendering="crispEdges"` keeps module
 * borders hard when the browser rasterizes for print.
 */
export function renderQrSvg(payload: string): string {
  const qr = create(payload, { errorCorrectionLevel: 'M' });
  const size = qr.modules.size;
  const get = (row: number, col: number): number => qr.modules.get(row, col);
  const total = size + QUIET_ZONE * 2;
  const path = toModulePath(get, size);

  return (
    `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${total} ${total}" ` +
    `shape-rendering="crispEdges" width="100%" height="100%" role="img">` +
    `<rect width="${total}" height="${total}" fill="#ffffff"/>` +
    `<path d="${path}" fill="#000000"/>` +
    `</svg>`
  );
}
