import { useRef, useState, type FormEvent } from 'react';
import { Download, FileText, Loader2, Paperclip, Upload } from 'lucide-react';
import { Button } from '@/shared/components/ui/button';
import { Modal } from '@/shared/components/ui/modal';
import { Input } from '@/shared/components/ui/input';
import { Label } from '@/shared/components/ui/label';
import { useToast } from '@/shared/components/ui/toast';
import type { ApiError } from '@/shared/types/api';
import { RequirePermission } from '@/modules/auth/PermissionsProvider';
import { Permissions } from '@/modules/auth/permissions';
import { formatDate } from '@/modules/in-vivo/presentation';
import {
  downloadAttachment,
  useAttachEvidence,
  useAttachments,
} from '@/modules/in-vivo/api/attachments.queries';
import type { AttachmentTargetKind } from '@/modules/in-vivo/types';

/** Renders a byte size compactly (e.g. "1.2 MB"), for the attachment metadata line. */
function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  const units = ['KB', 'MB', 'GB'];
  let value = bytes / 1024;
  let unitIndex = 0;
  while (value >= 1024 && unitIndex < units.length - 1) {
    value /= 1024;
    unitIndex += 1;
  }
  return `${Number(value.toFixed(1))} ${units[unitIndex]}`;
}

interface AttachmentsPanelProps {
  /** The study animal the evidence belongs to. */
  animalId: string;
  /** Whether the evidence documents a sample analysis or an experiment reading. */
  targetKind: AttachmentTargetKind;
  /** The owning aggregate id (biobank sample id or behavioural experiment id) that proves the link. */
  ownerId: string;
  /** The reading/analysis id the evidence documents. */
  targetId: string;
  /** A short human label for the target (e.g. the analysis name), shown in the upload dialog. */
  targetLabel: string;
}

/**
 * Evidence gallery + uploader for one reading/analysis (SISLAB-09). Lists the attachments tied to the given
 * animal+target (photo/PDF of a hemogram laudo, external-reader capture, …) with a download action, and — gated by
 * `Attachments.Attach` — an upload dialog that captures the file and a free-text provenance label (e.g. "Fiocruz").
 */
export function AttachmentsPanel({
  animalId,
  targetKind,
  ownerId,
  targetId,
  targetLabel,
}: AttachmentsPanelProps) {
  const [uploading, setUploading] = useState(false);
  const toast = useToast();
  const attachments = useAttachments({ animalId, targetKind, targetId });

  async function handleDownload(attachmentId: string, fileName: string) {
    try {
      await downloadAttachment(attachmentId, fileName);
    } catch (err) {
      toast('error', (err as ApiError)?.message ?? 'Não foi possível baixar o arquivo.');
    }
  }

  return (
    <div className="mt-2 rounded-md border bg-muted/20 p-3">
      <div className="mb-2 flex items-center justify-between">
        <span className="flex items-center gap-1.5 text-xs font-medium uppercase tracking-wide text-muted-foreground">
          <Paperclip className="size-3.5" />
          Evidências
        </span>
        <RequirePermission code={Permissions.attachments.attach}>
          <Button variant="ghost" size="sm" onClick={() => setUploading(true)}>
            <Upload className="size-4" />
            Anexar
          </Button>
        </RequirePermission>
      </div>

      {attachments.isLoading ? (
        <div className="flex items-center gap-2 py-2 text-sm text-muted-foreground">
          <Loader2 className="size-4 animate-spin" />
          Carregando anexos…
        </div>
      ) : attachments.isError ? (
        <p className="py-2 text-sm text-destructive">
          Não foi possível carregar os anexos.
        </p>
      ) : attachments.data && attachments.data.length > 0 ? (
        <ul className="divide-y">
          {attachments.data.map((attachment) => (
            <li
              key={attachment.id}
              className="flex items-center justify-between gap-3 py-2"
            >
              <div className="flex min-w-0 items-center gap-2">
                <FileText className="size-4 shrink-0 text-muted-foreground" />
                <div className="min-w-0">
                  <p className="truncate text-sm font-medium">{attachment.fileName}</p>
                  <p className="truncate text-xs text-muted-foreground">
                    {attachment.origin ? `${attachment.origin} · ` : ''}
                    {formatBytes(attachment.sizeBytes)} · {attachment.uploadedBy} ·{' '}
                    {formatDate(attachment.uploadedAtUtc)}
                  </p>
                </div>
              </div>
              <Button
                variant="ghost"
                size="sm"
                onClick={() => handleDownload(attachment.id, attachment.fileName)}
              >
                <Download className="size-4" />
              </Button>
            </li>
          ))}
        </ul>
      ) : (
        <p className="py-2 text-sm text-muted-foreground">Nenhuma evidência anexada.</p>
      )}

      {uploading && (
        <UploadEvidenceModal
          animalId={animalId}
          targetKind={targetKind}
          ownerId={ownerId}
          targetId={targetId}
          targetLabel={targetLabel}
          onClose={() => setUploading(false)}
        />
      )}
    </div>
  );
}

function UploadEvidenceModal({
  animalId,
  targetKind,
  ownerId,
  targetId,
  targetLabel,
  onClose,
}: AttachmentsPanelProps & { onClose: () => void }) {
  const attach = useAttachEvidence();
  const toast = useToast();
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [file, setFile] = useState<File | null>(null);
  const [origin, setOrigin] = useState('');

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!file) {
      toast('error', 'Selecione um arquivo para anexar.');
      return;
    }

    try {
      await attach.mutateAsync({
        animalId,
        targetKind,
        ownerId,
        targetId,
        origin: origin.trim() || null,
        file,
      });
      toast('success', 'Evidência anexada com sucesso.');
      onClose();
    } catch (err) {
      toast(
        'error',
        (err as ApiError)?.message ?? 'Não foi possível anexar a evidência.',
      );
    }
  }

  return (
    <Modal
      open
      onClose={onClose}
      title="Anexar evidência"
      description={`Foto/PDF do laudo ou leitora externa para "${targetLabel}".`}
      footer={
        <>
          <Button variant="outline" onClick={onClose} disabled={attach.isPending}>
            Cancelar
          </Button>
          <Button
            type="submit"
            form="upload-evidence-form"
            disabled={attach.isPending || !file}
          >
            {attach.isPending && <Loader2 className="size-4 animate-spin" />}
            Anexar
          </Button>
        </>
      }
    >
      <form
        id="upload-evidence-form"
        className="flex flex-col gap-4"
        onSubmit={handleSubmit}
        noValidate
      >
        <div className="flex flex-col gap-2">
          <Label htmlFor="evidence-file">Arquivo</Label>
          <input
            ref={fileInputRef}
            id="evidence-file"
            type="file"
            accept="image/*,application/pdf"
            onChange={(e) => setFile(e.target.files?.[0] ?? null)}
            className="block w-full text-sm text-muted-foreground file:mr-3 file:rounded-md file:border file:border-input file:bg-transparent file:px-3 file:py-1.5 file:text-sm file:font-medium hover:file:bg-muted"
            required
          />
          <p className="text-xs text-muted-foreground">
            Imagens ou PDF do laudo/leitura.
          </p>
        </div>
        <div className="flex flex-col gap-2">
          <Label htmlFor="evidence-origin">Origem (opcional)</Label>
          <Input
            id="evidence-origin"
            placeholder="Fiocruz / externo"
            value={origin}
            onChange={(e) => setOrigin(e.target.value)}
          />
          <p className="text-xs text-muted-foreground">
            Rótulo de procedência (ex.: Fiocruz, Leitora externa). Nunca fixo no código.
          </p>
        </div>
      </form>
    </Modal>
  );
}
