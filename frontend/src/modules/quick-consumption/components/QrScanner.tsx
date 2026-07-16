import { useState, type FormEvent } from 'react';
import { CameraOff, Keyboard, Loader2, ScanLine } from 'lucide-react';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import { useQrScanner, type ScannerError } from '@/modules/quick-consumption/hooks/useQrScanner';

interface QrScannerProps {
  /** Whether the camera should be decoding — paused by the parent once an item is loaded. */
  active: boolean;
  /** Raw decoded (or manually entered) QR text; the parent parses and resolves it. */
  onScan: (text: string) => void;
  /** Optional message shown under the frame, e.g. "QR não reconhecido" after a bad scan. */
  hint?: string | null;
}

const CAMERA_ERROR_COPY: Record<ScannerError, string> = {
  'permission-denied':
    'Acesso à câmera negado. Autorize a câmera nas permissões do navegador ou informe o código manualmente.',
  'no-camera': 'Nenhuma câmera disponível neste dispositivo. Informe o código manualmente.',
  'insecure-context':
    'A câmera exige uma conexão segura (HTTPS). Informe o código manualmente por enquanto.',
  unknown: 'Não foi possível iniciar a câmera. Informe o código manualmente.',
};

/**
 * Dumb QR capture surface: a live camera preview that decodes `sislab:item:<guid>` labels, with a
 * keyboard fallback (deep-link or id) for when the camera is blocked, missing or the origin is insecure.
 * All camera lifecycle lives in {@link useQrScanner}; this component only renders and forwards the text.
 */
export function QrScanner({ active, onScan, hint }: QrScannerProps) {
  const { videoRef, error, starting } = useQrScanner({ active, onDecode: onScan });
  const [manualOpen, setManualOpen] = useState(false);

  return (
    <div className="space-y-3">
      <div className="relative aspect-square w-full overflow-hidden rounded-2xl border bg-black">
        {/* Live camera preview — no <track>, since a getUserMedia video stream carries no captions. */}
        <video
          ref={videoRef}
          className="size-full object-cover"
          muted
          playsInline
          aria-label="Pré-visualização da câmera"
        />

        {error ? (
          <div className="absolute inset-0 flex flex-col items-center justify-center gap-2 bg-black/80 p-6 text-center text-sm text-white">
            <CameraOff className="size-8 opacity-80" />
            <p>{CAMERA_ERROR_COPY[error]}</p>
          </div>
        ) : (
          <>
            {/* Reticle overlay to guide aiming at the label. */}
            <div className="pointer-events-none absolute inset-0 flex items-center justify-center">
              <div className="size-3/5 rounded-xl border-2 border-white/70 shadow-[0_0_0_9999px_rgba(0,0,0,0.35)]" />
            </div>
            {starting && (
              <div className="absolute inset-0 flex items-center justify-center bg-black/50 text-white">
                <Loader2 className="size-6 animate-spin" />
              </div>
            )}
            {active && !starting && (
              <div className="absolute inset-x-0 bottom-0 flex items-center justify-center gap-2 bg-gradient-to-t from-black/70 to-transparent p-3 text-xs text-white">
                <ScanLine className="size-4" />
                Aponte a câmera para o QR do item
              </div>
            )}
          </>
        )}
      </div>

      {hint && (
        <p role="status" className="text-center text-sm font-medium text-destructive">
          {hint}
        </p>
      )}

      {manualOpen ? (
        <ManualEntry onSubmit={onScan} onCancel={() => setManualOpen(false)} />
      ) : (
        <Button
          type="button"
          variant="outline"
          className="w-full"
          onClick={() => setManualOpen(true)}
        >
          <Keyboard />
          Informar código manualmente
        </Button>
      )}
    </div>
  );
}

/** Keyboard fallback: paste a `sislab:item:<guid>` deep-link or a raw id when the camera is unavailable. */
function ManualEntry({
  onSubmit,
  onCancel,
}: {
  onSubmit: (text: string) => void;
  onCancel: () => void;
}) {
  const [value, setValue] = useState('');

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const trimmed = value.trim();
    if (trimmed) onSubmit(trimmed);
  }

  return (
    <form onSubmit={handleSubmit} noValidate className="space-y-2 rounded-lg border bg-muted/30 p-3">
      <label htmlFor="manual-qr" className="text-sm font-medium leading-none">
        Código do item
      </label>
      <Input
        id="manual-qr"
        value={value}
        onChange={(e) => setValue(e.target.value)}
        placeholder="sislab:item:<id> ou o id do item"
        autoFocus
        autoCapitalize="off"
        autoCorrect="off"
        spellCheck={false}
      />
      <div className="flex gap-2">
        <Button type="submit" size="sm" className="flex-1" disabled={!value.trim()}>
          Buscar item
        </Button>
        <Button type="button" size="sm" variant="ghost" onClick={onCancel}>
          Cancelar
        </Button>
      </div>
    </form>
  );
}
