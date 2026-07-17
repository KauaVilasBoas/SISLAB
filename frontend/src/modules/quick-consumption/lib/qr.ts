/**
 * The QR grammar the quick-consumption flow reads (cards [E7] #63 / #92) now lives in `shared/lib/qr`,
 * because the labels module also *writes* these payloads and the two features must share one grammar.
 * This module re-exports it so existing quick-consumption imports keep working after the promotion.
 */
export {
  type ScannedQr,
  type ScannedQrKind,
  encodeStockItemQr,
  encodeStorageLocationQr,
  parseStockItemQr,
  parseStorageLocationQr,
  resolveScannedQr,
  resolveStockItemId,
} from '@/shared/lib/qr';
