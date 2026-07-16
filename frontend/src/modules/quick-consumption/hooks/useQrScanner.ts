import { useEffect, useRef, useState } from 'react';
import { BrowserQRCodeReader, type IScannerControls } from '@zxing/browser';

/** Why the camera preview is not running, so the UI can show the right message and fallback. */
export type ScannerError = 'permission-denied' | 'no-camera' | 'insecure-context' | 'unknown';

interface UseQrScannerOptions {
  /** Whether the scanner should be actively decoding (paused once an item is loaded / on success). */
  active: boolean;
  /** Called with the raw decoded text on each successful decode; the caller decides what to do with it. */
  onDecode: (text: string) => void;
}

interface UseQrScannerResult {
  /** Ref to attach to the <video> preview element. */
  videoRef: React.RefObject<HTMLVideoElement>;
  /** Null while the camera is (re)starting or running; set when it could not start. */
  error: ScannerError | null;
  /** True between "asked for the camera" and "preview is running / failed". */
  starting: boolean;
}

/**
 * Drives a continuous QR scan over the rear camera (card [E7] #63).
 *
 * Encapsulates the whole getUserMedia lifecycle so the page stays declarative: it requests the
 * `environment`-facing camera through zxing's `decodeFromConstraints` (which calls getUserMedia and wires
 * the stream into the <video> element), reports a typed {@link ScannerError} when the browser blocks or
 * lacks a camera, and — crucially — stops the media stream whenever the hook goes inactive or unmounts, so
 * the camera light never stays on after a scan is confirmed or the operator leaves the screen.
 *
 * The decode callback fires many times per second; zxing reports "no QR in this frame" as an error we
 * ignore. Only a successful decode (a `Result`) reaches `onDecode`. The callback is read through a ref so a
 * changing handler identity does not tear down and restart the camera.
 */
export function useQrScanner({ active, onDecode }: UseQrScannerOptions): UseQrScannerResult {
  const videoRef = useRef<HTMLVideoElement>(null);
  const onDecodeRef = useRef(onDecode);
  onDecodeRef.current = onDecode;

  const [error, setError] = useState<ScannerError | null>(null);
  const [starting, setStarting] = useState(false);

  useEffect(() => {
    if (!active) return;

    // getUserMedia is only exposed on secure origins (https / localhost). Fail fast with a clear reason
    // instead of a cryptic exception when the SPA is served over plain http on a phone.
    if (!window.isSecureContext || !navigator.mediaDevices?.getUserMedia) {
      setError('insecure-context');
      return;
    }

    let controls: IScannerControls | null = null;
    let cancelled = false;

    setStarting(true);
    setError(null);

    const reader = new BrowserQRCodeReader();

    reader
      .decodeFromConstraints(
        { video: { facingMode: { ideal: 'environment' } } },
        videoRef.current ?? undefined,
        (result) => {
          if (result) onDecodeRef.current(result.getText());
        },
      )
      .then((scannerControls) => {
        // Component may have unmounted / gone inactive while getUserMedia was resolving — stop at once.
        if (cancelled) {
          scannerControls.stop();
          return;
        }
        controls = scannerControls;
        setStarting(false);
      })
      .catch((err: unknown) => {
        if (cancelled) return;
        setStarting(false);
        setError(classifyCameraError(err));
      });

    return () => {
      cancelled = true;
      controls?.stop();
    };
  }, [active]);

  return { videoRef, error, starting };
}

/** Maps a getUserMedia rejection to a {@link ScannerError} for a tailored, non-technical message. */
function classifyCameraError(err: unknown): ScannerError {
  const name = err instanceof Error ? err.name : '';
  if (name === 'NotAllowedError' || name === 'SecurityError') return 'permission-denied';
  if (name === 'NotFoundError' || name === 'OverconstrainedError' || name === 'NotReadableError') {
    return 'no-camera';
  }
  return 'unknown';
}
